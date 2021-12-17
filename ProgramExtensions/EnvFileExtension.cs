//
// Licensed under the Apache License, Version 2.0.
// See https://github.com/aspnet/Configuration/blob/master/LICENSE.txt for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DotNetEnv;
using Microsoft.Extensions.Configuration;

namespace DroHub.ProgramExtensions {
    public static class EnvFileExtension {
        public static IConfigurationBuilder LoadFromEnvFile(this IConfigurationBuilder builder,
            string path, string prefix, LoadOptions options) {
            return builder.Add(new EnvFileConfigurationSource(path, prefix, options));
        }
    }

    public class EnvFileConfigurationSource : IConfigurationSource {
        private readonly LoadOptions _load_options;
        private readonly string _path;
        private readonly string _prefix;

        public EnvFileConfigurationSource(string path, string prefix, LoadOptions load_options) {
            _load_options = load_options;
            _path = path;
            _prefix = prefix;
        }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new EnvFileConfigurationProvider(_path, _prefix, _load_options);
        }
    }
    public class EnvFileConfigurationProvider : ConfigurationProvider{
        private readonly LoadOptions _options;
        private readonly string _path;
        private readonly string _prefix;

        public EnvFileConfigurationProvider(string path, string prefix, LoadOptions options) {
            _options = options;
            _path = path;
            _prefix = prefix;
        }

        public override void Load() {
            var env = Env.Load(_path, _options).ToDictionary();

            var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var filtered_variables = env
                .SelectMany(NormalizeKey)
                .Where(entry => ((string)entry.Key).StartsWith(_prefix, StringComparison.OrdinalIgnoreCase));

            foreach (var envVariable in filtered_variables)
            {
                var key = ((string)envVariable.Key)[_prefix.Length..];
                data[key] = (string)envVariable.Value;
            }

            Data = data;
        }

        private static IEnumerable<DictionaryEntry> NormalizeKey(KeyValuePair<string, string> e) {
            var (key, value) = e;
            yield return new DictionaryEntry() {
                Key = NormalizeKey(key),
                Value = value
            };
        }

        private static string NormalizeKey(string key)
        {
            return key.Replace("__", ConfigurationPath.KeyDelimiter);
        }
    }
}