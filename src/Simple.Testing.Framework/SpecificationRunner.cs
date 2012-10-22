using System;
using System.Linq;
using System.Reflection;
using PowerAssert;
using Simple.Testing.ClientFramework;

namespace Simple.Testing.Framework
{
    public class SpecificationRunner
    {
        public RunResult RunSpecifciation(SpecificationToRun spec)
        {
            if (!spec.IsRunnable)
            {
                return new RunResult
                           {
                                  FoundOnMemberInfo = spec.FoundOn,
                                  Message = spec.Reason,
                                  Thrown = spec.Exception,
                                  Passed = false
                              };
            }
            var method = typeof(SpecificationRunner).GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Instance);
            var tomake = spec.Specification.GetType().GetInterfaces().Single(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(TypedSpecification<>));
            var generic = method.MakeGenericMethod(tomake.GetGenericArguments()[0]);
            var result = (RunResult) generic.Invoke(this, new object[] {spec.Specification, spec.FoundOn});
            result.FoundOnMemberInfo = spec.FoundOn;
            return result;
        }

        private RunResult Run<T>(TypedSpecification<T> spec, MemberInfo foundon)
        {
            var result = new RunResult { SpecificationName = spec.GetName()};
            RunResult runResult;
            object sut;
            object whenResult;
            Delegate when;

            if (RunSetup(spec, result, out runResult, out sut, out whenResult, out when)) return runResult;
            var fromWhen = when.Method.ReturnType == typeof(void) ? sut : whenResult;
	    bool allOkForAssertions = true;
	    bool allOkForTeardowns = true; 
            //OK THIS IS A HACK! ITS HERE FOR PROFILING SUPPORT (I NEED TO BE ABLE TO GET THE METHOD OF THE ORIGINAL METHOD OF SPEC)
            var hack = foundon as MethodInfo;
            if (hack != null)
            {
                hack.CallMethod();
            }
            //YOU CANNOT PUT A METHOD CALL BETWEEN LAST AND THIS ONE!!!!
            allOkForAssertions = RunAssertions(spec, result, fromWhen);
            allOkForTeardowns = RunTeardowns(spec, result);
            result.Passed = allOkForAssertions && allOkForTeardowns;
            return result;
        }

        private static bool RunTeardowns<T>(TypedSpecification<T> spec, RunResult result)
        {
            bool allOk = true;
            try
            {
                var Finally = spec.GetFinally();
                Finally.InvokeIfNotNull();
            }
            catch (Exception ex)
            {
                allOk = false;
                result.Message = "Finally failed";
                result.Thrown = ex.InnerException;
            }
            return allOk;
        }

        private static bool RunAssertions<T>(TypedSpecification<T> spec, RunResult result, object fromWhen)
        {
            bool allOk = true;
            foreach (var exp in spec.GetAssertions())
            {
                var partiallyApplied = PartialApplicationVisitor.Apply(exp, fromWhen);
                try
                {
                    PAssert.IsTrue(partiallyApplied);
                    result.Expectations.Add(new ExpectationResult
                                                {
                                                    Passed = true,
                                                    Text = PAssert.CreateSimpleFormatFor(partiallyApplied),
                                                    OriginalExpression = exp
                                                });
                }
                catch (Exception ex)
                {
                    allOk = false;
                    result.Expectations.Add(new ExpectationResult
                                                {
                                                    Passed = false,
                                                    Text = PAssert.CreateSimpleFormatFor(partiallyApplied),
                                                    OriginalExpression = exp,
                                                    Exception = ex
                                                });
                }
            }
            return allOk;
        }

        private static bool RunSetup<T>(TypedSpecification<T> spec, RunResult result, out RunResult runResult, out object sut,
                                         out object whenResult, out Delegate when)
        {
            when = null;
            sut = null;
            whenResult = null;
            runResult = null;
            try
            {
                var before = spec.GetBefore();
                before.InvokeIfNotNull();
            }
            catch (Exception ex)
            {
                result.MarkFailure("Before Failed", ex.InnerException);
                {
                    runResult = result;
                    
                    return true;
                }
            }
            sut = null;
            try
            {
                var given = spec.GetOn();
                sut = given.DynamicInvoke();
                result.On = given;
            }
            catch (Exception ex)
            {
                result.MarkFailure("On Failed", ex.InnerException);
            }
            whenResult = null;
            try
            {
                when = spec.GetWhen();
                if (when == null)
                {
                    runResult = new RunResult
                                    {SpecificationName = spec.GetName(), Passed = false, Message = "No when on specification"};
                    return true;
                }
                if (when.Method.GetParameters().Length == 1)
                    whenResult = when.DynamicInvoke(new[] {sut});
                else
                    whenResult = when.DynamicInvoke();
                if (when.Method.ReturnType != typeof (void))
                    result.Result = whenResult;
                else
                    result.Result = sut;
            }
            catch (Exception ex)
            {
                result.MarkFailure("When Failed", ex.InnerException);
                {
                    runResult = result;
                    return true;
                }
            }
            return false;
        }
    }

}
