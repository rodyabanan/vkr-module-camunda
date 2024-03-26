using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Camunda.Worker;
using Camunda.Worker.Variables;
using CsEx.Option;
using JasperFx.Core.Reflection;
using LanguageExt;
using MassTransit;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json;
using TaskManager.Controllers.v2;
using TaskManager.Domain.Sagas.Ltps;
using TaskManager.Domain.TaskApiGenerator;
using static HotChocolate.ErrorCodes;
using TaskManager.Domain.Executing;

namespace TaskManager.Domain.Integration.Camunda;


[HandlerTopics("SagaRunHandler")]
public class SagaRunHandler : IExternalTaskHandler
{
    private readonly IBus _bus;
    public SagaRunHandler(IBus bus)
    {
        _bus = bus;
    }
    public async Task<IExecutionResult> HandleAsync(ExternalTask externalTask, CancellationToken cancellationToken)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var types = assembly.GetTypes();

        var nameOfSaga = externalTask.GetOrFail<string>("NameOfSaga");

        var typeAliases = externalTask.Variables
            .Where(v => v.Key.StartsWith("type_"))
            .ToDictionary(x => x.Key.Split("_")[1], x => x.Key);


        var classType = types.FirstOrDefault(type => string.Equals(type.Name, nameOfSaga, StringComparison.InvariantCultureIgnoreCase))
                                ?? throw new ArgumentException($"Saga by name {nameOfSaga} not found");

        
        var constructors = classType.GetConstructors();

        var corrId = Guid.NewGuid();

        var defaultValuesFunctions = new Dictionary<string, Func<object>>()
        {
            {"userToken", () => "" },
            {"correlationId", () => corrId }
        };

        var constructor = constructors.First();

        List<object> paramaters = new();

        foreach (var param in constructor.GetParameters())
        {
            if (defaultValuesFunctions.ContainsKey(param.Name!))
            {
                paramaters.Add(defaultValuesFunctions[param.Name!].Invoke());
            }
            else if (param.HasDefaultValue)
            {
                paramaters.Add(param.DefaultValue!);
            }
            else if (param.ParameterType.IsClass)
            {
                paramaters.Add(FillTmCommand(externalTask, param.ParameterType));
            }
            else if (param.ParameterType.IsBoolean())
            {
                paramaters.Add(false);
            }
            else if (param.ParameterType.IsInterface && typeAliases.TryGetValue(param.Name, out var paramTypeKey))
            {
                var parameterTypeName = externalTask.GetOrFail<string>(paramTypeKey);
                var parameterType = types.FirstOrDefault(x => x.Name == parameterTypeName);
                paramaters.Add(FillTmCommand(externalTask, parameterType));
            }
            else
            {
                throw new Exception($"Непредусмотренна обработка для параметра {param.Name}");
            }
        }
        
        var startDto = Activator.CreateInstance(classType, paramaters.ToArray())!;

        await _bus.Publish(startDto);


        return new CompleteResult
        {
            Variables = new Dictionary<string, VariableBase>
             {
                 {
                     "TaskId", new StringVariable(corrId.ToString())
                 }
             }
        };
    }

    private static object FillTmCommand(ExternalTask externalTask, Type type, object? dto = null)
    {
        dto ??= Activator.CreateInstance(type);

        var properties = type.GetProperties();

        foreach (var property in properties)
        {
            var fieldName = property.Name;

            var value = externalTask.GetOrFail(fieldName, property.PropertyType);

            property.SetValue(dto, value);
        }
        if (type.BaseType != null)
        {
            return FillTmCommand(externalTask, type.BaseType, dto);
        }
        return dto!;
    }
}