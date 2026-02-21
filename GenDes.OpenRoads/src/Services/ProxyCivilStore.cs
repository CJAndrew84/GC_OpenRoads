using System;
using System.Collections.Generic;

namespace GenDes_OpenRoads.Services
{
    internal sealed class ProxyCivilObject
    {
        public string Id;
        public string Name;
        public string Kind;
        public object Payload;
        public bool Persist;
        public object Element;
    }

    internal static class ProxyCivilStore
    {
        private static readonly Dictionary<string, ProxyCivilObject> _objects = new Dictionary<string, ProxyCivilObject>(StringComparer.OrdinalIgnoreCase);

        public static ProxyCivilObject Upsert(string stableKey, string kind, string name, object payload, bool persist)
        {
            ProxyCivilObject existing;
            if (!_objects.TryGetValue(stableKey, out existing))
            {
                existing = new ProxyCivilObject
                {
                    Id = stableKey,
                    Kind = kind,
                    Name = name,
                    Persist = persist
                };
                _objects[stableKey] = existing;
            }

            existing.Payload = payload;
            existing.Name = name;
            existing.Persist = persist;
            return existing;
        }

        public static bool TryGet(string id, out ProxyCivilObject value)
        {
            return _objects.TryGetValue(id ?? string.Empty, out value);
        }
    }
}
