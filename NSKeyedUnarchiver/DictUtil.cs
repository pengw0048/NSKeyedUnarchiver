using System;
using System.Collections.Generic;

namespace NSKeyedUnarchiver
{
    // This class provides static methods to access the canonical representation of structs
    // returned from `Unarchiver.DeepParse`. Think of a Json object:
    //    root = {"intval": 42, "array": [{"inner": "good"}, true]}
    // However, in our representation, classes are `Dictionary<string, object>`, arrays are
    // `[]object`, and basic types are `object`. It is painful to do type conversion all the time.
    // Use this helper class to safe your day:
    //    var succ = DictUtil.TryGet<int>(root, "intval", out var intval);
    //    succ = DictUtil.TryGetInArray<bool>(root, "array", 1, out var isInnerGood);
    //    succ = DictUtil.TryGetInArray(root, "array", 0, out var innerObject);
    //    if (succ) succ = DictUtil.TryGet<string>(innerObject, "inner", out var inner);
    public static class DictUtil
    {
        public static bool TryGet<T>(object obj, string key, out T value)
        {
            if (obj.GetType().Equals(typeof(Dictionary<string, object>)))
            {
                var objDict = (Dictionary<string, object>)obj;
                if (objDict.ContainsKey(key))
                {
                    var target = objDict[key];
                    try {
                        value = (T)Convert.ChangeType(target, typeof(T));
                        return true;
                    }
                    catch (Exception) { }
                }
            }
            value = default(T);
            return false;
        }

        public static bool TryGet(object obj, string key, out object value)
        {
            return TryGet<object>(obj, key, out value);
        }

        public static bool TryGetSubclass(object obj, string key, out Dictionary<string, object> value)
        {
            return TryGet(obj, key, out value);
        }

        public static bool TryGetInArray<T>(object obj, string key, int index, out T value)
        {
            var ok = TryGet<T[]>(obj, key, out var array);
            if (ok && index < array.Length)
            {
                value = array[index];
                return true;
            }
            value = default(T);
            return false;
        }

        public static object TryGetInArray(object obj, string key, int index, out object value)
        {
            return TryGetInArray<object>(obj, key, index, out value);
        }
    }
}
