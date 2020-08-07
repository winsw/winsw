using System;
using System.Collections.Generic;
using System.IO;

namespace WinSW.Util
{
    public class ObjectQuery
    {
        private readonly object configObject;
        private object? current;
        private string? key;

        public ObjectQuery(object? config)
        {
            if (config is null)
            {
                throw new InvalidDataException("Query object is null");
            }

            this.configObject = config;
            this.current = this.configObject;
        }

        public ObjectQuery On(string key)
        {
            this.key = key;
            this.current = this.Query(this.configObject, key);
            return this;
        }

        public ObjectQuery Get(string key)
        {
            if (this.current == null)
            {
                throw new InvalidDataException("The key <" + key + "> is not exist");
            }

            this.key = key;
            this.current = this.Query(this.current, key);
            return this;
        }

        public new string ToString()
        {
            if (this.current == null)
            {
                throw new InvalidDataException("The key <" + this.key + "> is not exist");
            }

            var result = this.current as string;

            if (result == null)
            {
                throw new InvalidDataException(this.key + " can't converto to a string");
            }

            return result;
        }

        public List<T> ToList<T>()
        {
            if (this.current == null)
            {
                throw new InvalidDataException("The key <" + this.key + "> is not exist");
            }

            var list = this.current as List<object>;

            if (list == null)
            {
                throw new InvalidDataException(this.key + " can't converto to List<" + typeof(T) + ">");
            }

            var result = new List<T>(0);
            foreach (var item in list)
            {
                result.Add((T)item);
            }

            return result;
        }

        public bool ToBoolean()
        {
            if (this.current == null)
            {
                throw new InvalidDataException("The key <" + this.key + "> is not exist");
            }

            var value = this.current as string;

            if (value == null)
            {
                throw new InvalidDataException(this.key + " can't convert into bool");
            }

            if (value == "true" || value == "yes" || value == "on")
            {
                return true;
            }
            else if (value == "false" || value == "no" || value == "off")
            {
                return false;
            }
            else
            {
                throw new InvalidDataException(value + " cannot convert into bool");
            }
        }

        public ObjectQuery At(int index)
        {
            if (this.current == null)
            {
                throw new InvalidDataException("The key <" + this.key + "> is not exist");
            }

            var list = this.current as List<object>;

            if (list == null)
            {
                throw new InvalidDataException("Can't execute At(index) on " + this.key);
            }

            try
            {
                var result = list[index];
                this.current = result;
            }
            catch (IndexOutOfRangeException)
            {
                throw new InvalidDataException("Index " + index + " not in range");
            }

            return this;
        }

        private object? Query(object dic, string key)
        {
            if (dic == null)
            {
                throw new InvalidDataException(key + " is not found");
            }

            var dict = dic as IDictionary<object, object>;
            if (dict != null)
            {
                foreach (KeyValuePair<object, object> kvp in dict)
                {
                    if (kvp.Key as string == key)
                    {
                        return kvp.Value;
                    }
                }
            }

            return null;
        }
    }
}
