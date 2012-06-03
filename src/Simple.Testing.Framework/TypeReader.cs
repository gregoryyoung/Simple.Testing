using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Simple.Testing.ClientFramework;

namespace Simple.Testing.Framework
{
    public static class TypeReader
    {
        public static IEnumerable<SpecificationToRun> GetSpecificationsIn(Type t)
        {
            foreach (var methodSpec in AllMethodSpecifications(t)) yield return methodSpec;
            foreach (var fieldSpec in AllFieldSpecifications(t)) yield return fieldSpec;
        }

        private static IEnumerable<SpecificationToRun> AllMethodSpecifications(Type t)
        {
            foreach (var s in t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                SpecificationToRun toRun = null;
                if (typeof(Specification).IsAssignableFrom(s.ReturnType))
                {
                    try
                    {
                        var result = s.CallMethod();
                        
                        if (result != null) toRun = new SpecificationToRun((Specification)result, s);
                    }
                    catch (Exception ex)
                    {
                        toRun = new SpecificationToRun(null, "Exception when creating specification", ex, s);
                    }
                    yield return toRun;
                }
                if (typeof(IEnumerable<Specification>).IsAssignableFrom(s.ReturnType))
                {
                    var specsToRun = new List<SpecificationToRun>();
                    var specs = new List<Specification>();
                    IEnumerable<Specification> obj;
                    bool error = false;
                    try
                    {
                        obj = (IEnumerable<Specification>)s.CallMethod();
                        specs = obj.ToList();
                    }
                    catch (Exception ex)
                    {
                        specsToRun.Add(new SpecificationToRun(null, "Exception occured creating specification", ex, s));
                        error = true;
                    }
                    if (!error)
                    {
                        foreach (var item in specs)
                            yield return new SpecificationToRun(item, s);
                    }
                }
            }
        }

        private static IEnumerable<SpecificationToRun> AllFieldSpecifications(Type t)
        {
            foreach (var m in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (typeof(Specification).IsAssignableFrom(m.FieldType))
                {
                    var spec = (Specification) m.GetValue(Activator.CreateInstance(t));
                    if(spec != null)
                        yield return new SpecificationToRun(spec, m);
                }
                if (typeof(IEnumerable<Specification>).IsAssignableFrom(m.FieldType))
                {
                    var obj = (IEnumerable<Specification>)m.GetValue(Activator.CreateInstance(t));
                    if (obj != null)
                    {
                        foreach (var item in obj)
                            yield return new SpecificationToRun(item, m);
                    }
                }
            }
        }
    }
}