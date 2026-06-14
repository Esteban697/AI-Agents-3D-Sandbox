using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Agent_Abstractions.Memory
{
    public class StateHandler
    {
        private readonly string _filePath;

        public StateHandler(string filePath)
        {
            _filePath = filePath;
        }

        public Dictionary<string, int> LoadState()
        {
            string json = File.ReadAllText(_filePath);
            return JsonConvert.DeserializeObject<Dictionary<string, int>>(json)
                   ?? new Dictionary<string, int>();
        }

        public void SaveState(Dictionary<string, int> message)
        {
            string json = JsonConvert.SerializeObject(message, Formatting.Indented);
            File.WriteAllText(_filePath, json);
            Console.WriteLine($"File '{_filePath}' updated successfully.");
        }

        public Dictionary<string, int> UpdateState(string key, int increment)
        {
            var message = LoadState();
            message[key] = message.ContainsKey(key) ? message[key] + increment : increment;
            SaveState(message);
            return message;
        }
    }
}
