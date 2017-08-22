using Claunia.PropertyList;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NSKeyedUnarchiver
{
    public static class Unarchiver
    {
        // Translates a `NSKeyedArchiver` object, which resides in a `NSDictionary`, to a C#-styled object.
        public static Object DeepParse(NSObject rootObject)
        {
            var root = (NSDictionary)rootObject;
            var objects = (NSArray)(root.ObjectForKey("$objects"));
            var top = ConvertNSClasses(ConvertMutableClasses(LinkUID(objects, root.ObjectForKey("$top"))));
            return ((Dictionary<string, Object>)top)["root"];
        }

        // Recursively links all `CF$UID` entries to corresponding elements in `objects` array.
        public static NSObject LinkUID(NSArray objects, NSObject current)
        {
            switch (current)
            {
                case NSArray currentArray:
                    for (int i = 0; i < currentArray.Count; i++)
                    {
                        currentArray[i] = LinkUID(objects, currentArray[i]);
                    }
                    return currentArray;
                case NSDictionary currentDict:
                    var newDict = new NSDictionary();
                    foreach (var item in currentDict)
                    {
                        var key = item.Key;
                        var value = LinkUID(objects, item.Value);
                        newDict.Add(key, value);
                    }
                    return newDict;
                case NSSet currentSet:
                    var newArray = currentSet.AllObjects().Select(o => LinkUID(objects, o)).ToArray();
                    return new NSSet(NSSetSorted(currentSet), newArray);
                case UID currentUid:
                    return LinkUID(objects, objects[FromUID(currentUid)]);
                default:
                    return current;
            }
        }

        // Recursively converts `NS..` classes to C# classes.
        public static Object ConvertNSClasses(NSObject current)
        {
            switch (current)
            {
                case NSArray currentArray:
                    return currentArray.Select(o => ConvertNSClasses(o)).ToArray();
                case NSDictionary currentDict:
                    return currentDict.ToDictionary(p => p.Key, p => ConvertNSClasses(p.Value));
                case NSSet currentSet:
                    if (NSSetSorted(currentSet)) return new SortedSet<Object>(currentSet.AllObjects());
                    else return new HashSet<Object>(currentSet.AllObjects());
                case NSData currentData:
                    return currentData.Bytes;
                case NSDate currentDate:
                    return currentDate.Date;
                case NSNumber currentNumber:
                    switch (currentNumber.GetNSNumberType())
                    {
                        case NSNumber.BOOLEAN:
                            return currentNumber.ToBool();
                        case NSNumber.INTEGER:
                            return currentNumber.ToLong();
                        case NSNumber.REAL:
                            return currentNumber.ToDouble();
                        default:
                            return current;
                    }
                case NSString currentString:
                    return currentString.Content;
                case UID currentUid:
                    return FromUID(currentUid);
                default:
                    return current;
            }
        }

        // Recursively converts `NSMutable..` classes into corresponding ordinary ones.
        // Assume `current` comes from `ConvertNSClasses`, because keys can only be strings.
        public static NSObject ConvertMutableClasses(NSObject current)
        {
            switch (current)
            {
                case NSArray currentArray:
                    for (int i = 0; i < currentArray.Count; i++)
                    {
                        currentArray[i] = ConvertMutableClasses(currentArray[i]);
                    }
                    return currentArray;
                case NSDictionary currentDict:
                    switch (ClassName(currentDict))
                    {
                        case "NSMutableDictionary":
                            var nsKeys = (NSArray)currentDict["NS.keys"];
                            var nsObjects1 = (NSArray)currentDict["NS.objects"];
                            var newDict1 = new NSDictionary();
                            for (int i = 0; i < nsKeys.Count; i++)
                            {
                                var key = (string)(NSString)nsKeys[i];
                                var value = ConvertMutableClasses(nsObjects1[i]);
                                newDict1.Add(key, value);
                            }
                            return newDict1;
                        case "NSMutableArray":
                            var nsObjects = (NSArray)currentDict["NS.objects"];
                            var newArray2 = new NSArray();
                            for (int i = 0; i < nsObjects.Count; i++)
                            {
                                newArray2.Add(ConvertMutableClasses(nsObjects[i]));
                            }
                            return newArray2;
                        default:
                            var newDict = new NSDictionary();
                            foreach (var item in currentDict)
                            {
                                var key = item.Key;
                                var value = ConvertMutableClasses(item.Value);
                                newDict.Add(key, value);
                            }
                            return newDict;
                    }
                case NSSet currentSet:
                    var newArray = currentSet.AllObjects().Select(o => ConvertMutableClasses(o)).ToArray();
                    return new NSSet(NSSetSorted(currentSet), newArray);
                default:
                    return current;
            }
        }

        // The format uses a `NSDictionary` to represent a class, so this function tries to return its name.
        public static string ClassName(NSDictionary current)
        {
            if (current.ContainsKey("$class"))
            {
                var classDict = (NSDictionary)current.ObjectForKey("$class");
                if (classDict.ContainsKey("$classname"))
                {
                    return (string)(NSString)classDict.ObjectForKey("$classname");
                }
            }
            return null;
        }

        // Returns the number (ID) stored in a `UID` object.
        public static int FromUID(NSObject uid)
        {
            var bytes = ((UID)uid).Bytes;
            if (bytes.Length == 1) return bytes[0];
            if (bytes.Length == 2) return BitConverter.ToUInt16(bytes.Reverse().ToArray(), 0);
            return BitConverter.ToInt32(bytes.Reverse().ToArray(), 0);
        }

        // HACK: NSSet does not export `sorted` field; access it with reflection.
        private static bool NSSetSorted(NSSet set)
        {
            return (bool)(typeof(NSSet).GetField("ordered", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(set));
        }
    }
}
