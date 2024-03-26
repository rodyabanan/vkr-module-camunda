using Camunda.Worker;
using Camunda.Worker.Variables;
using QuikGraph.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Linq;
using LanguageExt.TypeClasses;
using MoreLinq;
using System.Threading.Tasks;
using CsEx.Option;
using LanguageExt;
using static System.Net.Mime.MediaTypeNames;
using System.ComponentModel;
using System.Globalization;
using Microsoft.Extensions.Caching.Memory;
using System.Reflection;

namespace TaskManager.Domain.Integration.Camunda
{
    public static class CamundaExtensions
    {
        private static readonly IMemoryCache _memoryCache = new MemoryCache(new MemoryCacheOptions());

        public static Option<object> GetOptional(this ExternalTask task, string id, Type type)
        {
            var culture = new CultureInfo("ru");

            if (task.Variables.TryGetValue(id, out var valueWrapper))
            {
                var wrapperType = valueWrapper.GetType();

                var valueProperty = _memoryCache.GetOrCreate(wrapperType.Name, e => wrapperType.GetProperty("Value"));

                var value = valueProperty.GetValue(valueWrapper)!;

                var converter = TypeDescriptor.GetConverter(type);

                if (converter.CanConvertTo(type))
                {
                    return converter.ConvertTo(value, type);
                }

                var textValue = value.ToString();

                if (type.IsArray)
                {

                    
                    var elementType = type.GetElementType()!;

                    var values = textValue.Split(',').Where(x => !string.IsNullOrEmpty(x)).ToArray();

                    var elementConverter = TypeDescriptor.GetConverter(elementType);

                    var arr = Array.CreateInstance(elementType, values.Length);

                    for (int i = 0; i < arr.Length; i++)
                    {
                        var element = elementConverter.ConvertFromString(null, culture, values[i]);
                        arr.SetValue(element, i);
                    }

                    return arr;
                }

                return converter.ConvertFromString(null, culture, textValue) ?? Option<object>.None;
            }

            return Option<object>.None;
        }

        public static Option<T> GetOptional<T>(this ExternalTask task, string id)
        {
            return GetOptional(task, id, typeof(T)).Match(x => (T)x, Option<T>.None);
        }

        public static object GetOrFail(this ExternalTask task, string propertyName, Type type) => GetOptional(task, propertyName, type).GetOrFall(() => $"Свойство {propertyName} не найдено");

        public static T GetOrFail<T>(this ExternalTask task, string propertyName) => GetOptional<T>(task, propertyName).GetOrFall(() => $"Свойство {propertyName} не найдено");
    }
}
