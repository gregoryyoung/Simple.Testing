using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Simple.Testing.ClientFramework;

namespace Simple.Testing.Framework
{
    public class NamedMethodsGenerator : ISpecificationGenerator
    {
        private readonly Assembly _assembly;
        private readonly List<String> _methods = new List<string>();
 
        public NamedMethodsGenerator(Assembly assembly, string method)
        {
            _assembly = assembly;
            _methods.Add(method);
        }

        public NamedMethodsGenerator(Assembly assembly, IEnumerable<string> methods)
        {
            _assembly = assembly;
            _methods.AddRange(methods);
        }

        public IEnumerable<SpecificationToRun> GetSpecifications()
        {
            foreach(var method in _methods)
            {
                var typename = GetTypeName(method);
                var methodname = GetMethodName(method);
                var type = _assembly.GetType(typename);
                if(type == null) throw new ArgumentException("Type not found", "method");
                var allMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
                var methodinfos = allMethods.Where(x => x.Name == methodname);
                foreach(var info in methodinfos)
                {
                    SpecificationToRun toRun = null;
                    if (typeof(Specification).IsAssignableFrom(info.ReturnType))
                    {
                        try
                        {
                            var result = info.CallMethod();
                            if (result != null)  toRun = new SpecificationToRun((Specification) result, info);
                        }
                        catch(Exception ex)
                        {
                            toRun = new SpecificationToRun(null, "Exception when creating specification", ex, info);
                        }
                        yield return toRun;
                    }
                    if (typeof(IEnumerable<Specification>).IsAssignableFrom(info.ReturnType))
                    {
                        var specsToRun = new List<SpecificationToRun>();
                        var specs = new List<Specification>();
                        IEnumerable<Specification> obj;
                        bool error = false;
                        try
                        {
                            obj = (IEnumerable<Specification>) info.CallMethod();
                            specs = obj.ToList();
                        }
                        catch(Exception ex)
                        {
                            specsToRun.Add(new SpecificationToRun(null, "Exception occured creating specification", ex, info));
                            error = true;
                        }
                        if(!error)
                        {
                            foreach (var item in specs)
                                yield return new SpecificationToRun(item, info);
                        }
                    }
                }
            }
        }

        private string GetMethodName(string method)
        {
            var lastdot = method.LastIndexOf(".");
            if (lastdot == -1) return null;
            return method.Substring(lastdot + 1, method.Length - lastdot - 1);
        }

        private string GetTypeName(string method)
        {
            var lastdot = method.LastIndexOf(".");
            if (lastdot == -1) return null;
            return method.Substring(0, lastdot);
        }
    }
}
