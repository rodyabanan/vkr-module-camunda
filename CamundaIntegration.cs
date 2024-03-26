using Camunda.Worker;
using Confluent.Kafka;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using System.Text;
using System.Text.Json;
using TaskManager.Domain.Executing;

namespace TaskManager.Domain.Integration.Camunda
{
    public class CamundaIntegrations
    {
        private readonly CamundaOptions _camundaOptions;
        private readonly HttpClient _httpClient;
        public CamundaIntegrations(IConfiguration config, HttpClient client) : base()
        {
            _httpClient = client;
            _camundaOptions = config.GetSection("Camunda").Get<CamundaOptions>() ?? throw new Exception("Конфигурация для камунды не определена");
            _httpClient.BaseAddress = _camundaOptions.Url;
        }

        /// <summary>
        /// Отправляет колбэк если сага успешно завершилась
        /// </summary>
        /// <param name="taskId">correlationId</param>
        /// <returns></returns>
        /// <exception cref="Exception">Status code is not 204</exception>
        public async Task SendCallback(Guid taskId)
        {
            if (!_camundaOptions.IsEnabled)
            {
                return;
            }

            var message = new
            {
                messageName = "msgOk",
                correlationKeys = new
                {
                    TaskId = new
                    {
                        value = taskId,
                        type = "String"
                    }
                }
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(message),
                Encoding.UTF8,
                "application/json"
            );
            var uri = "message";

            var resp = await _httpClient.PostAsync(uri, jsonContent);
            if (resp.StatusCode != System.Net.HttpStatusCode.NoContent)
            {
                throw new Exception("Status code is not 204");
            }
        }

        /// <summary>
        /// Отправляет колбэк если сага упала с ошибкой
        /// </summary>
        /// <param name="taskId">correlationId</param>
        /// <param name="error">Ошибка с которой упала сага</param>
        /// <returns></returns>
        /// <exception cref="Exception">Staus code is not 204</exception>
        public async Task SendFailedCallback(Guid taskId, string? error)
        {
            if (!_camundaOptions.IsEnabled)
            {
                return;
            }

            var message = new
            {
                messageName = "errorMsg",
                correlationKeys = new
                {
                    TaskId = new
                    {
                        value = taskId,
                        type = "String"
                    }
                },
                processVariables = new
                {
                    SagaRunErrorMessage = new
                    {
                        value = error,
                        type = "String"
                    }
                }
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(message),
                Encoding.UTF8,
                "application/json"
            );
            var uri = "message";

            var resp = await _httpClient.PostAsync(uri, jsonContent);
            if (resp.StatusCode != System.Net.HttpStatusCode.NoContent)
            {
                throw new Exception("Status code is not 204");
            }
        }


    }
}
