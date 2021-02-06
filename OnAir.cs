﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OnAirLight
{
    public class OnAir : BackgroundService
    {
        private IConfiguration _configuration;
        private ILogger _logger;
        private static readonly HttpClient _httpClient = new HttpClient();
        private bool _lastState = false;

        public OnAir(IConfiguration configuration, ILogger<OnAir> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Hello, World!");
            await SetHueLight(false);

            while (true)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                await Loop();
                await Task.Delay(1000, stoppingToken);
            }
        }

        private async Task Loop()
        {
            try
            {
                bool cameraInUse = GetCameraInUse();
                _logger.LogDebug($"camera in use: {cameraInUse}");

                if (_lastState != cameraInUse)
                {
                    await SetHueLight(cameraInUse);
                    _lastState = cameraInUse;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "");
            }
        }

        private bool GetCameraInUse(string keyName = null)
        {
            if (keyName == null)
            {
                keyName = @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam\";
            }

            var key = Registry.LocalMachine.OpenSubKey(keyName);

            var lastUsedTimeStop = (long)key.GetValue("LastUsedTimeStop", (long)-1);
            if (lastUsedTimeStop == 0)
            {
                _logger.LogInformation($"{keyName}: {lastUsedTimeStop}");
                return true;
            }

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                var camInUse = GetCameraInUse(keyName + subKeyName + "\\");
                if (camInUse) return true;
            }

            return false;
        }

        private async Task SetHueLight(bool on)
        {
            _logger.LogInformation($"Setting light: {on}");

            var username = _configuration["hue:username"];
            var hueBridgeIp = _configuration["hue:bridgeIp"];
            var lightNumber = _configuration["hue:lightNumber"];

            var url = $"http://{hueBridgeIp}/api/{username}/lights/{lightNumber}/state";
            var bodyJson = JsonSerializer.Serialize(new
            {
                on = on,
                sat = 254,
                bri = 254,
                hue = _configuration.GetValue<int>("onAirLightColor")
            });

            using (var content = new StringContent(bodyJson, Encoding.UTF8, "application/json"))
            using (var httpResponse = await _httpClient.PutAsync(url, content))
            {
                var responseContent = await httpResponse.Content.ReadAsStringAsync();
                _logger.LogInformation(responseContent);
                httpResponse.EnsureSuccessStatusCode();
            }
        }
    }
}
