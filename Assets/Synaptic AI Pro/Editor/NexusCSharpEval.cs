using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;

namespace SynapticPro
{
    /// <summary>
    /// Arbitrary C# evaluation for the Editor — equivalent of Blender's
    /// run_python tool. Wraps Mono.CSharp.Evaluator (instance API) so callers
    /// can execute any C# snippet against the running Editor without
    /// triggering an AssemblyReload.
    ///
    /// Static Evaluator.Init/Run on Unity 2022.3+ silently no-ops; the real
    /// path is `new Evaluator(new CompilerContext(new CompilerSettings(),
    /// new ConsoleReportPrinter()))` plus injecting every assembly already
    /// loaded in the AppDomain so UnityEngine / UnityEditor / Newtonsoft.Json
    /// resolve.
    ///
    /// Expressions ("1+1", "GameObject.Find(\"X\").name") must NOT end with
    /// a semicolon — those are evaluated via Evaluate(...). Statements
    /// ("var x = 1; Debug.Log(x);") run through Run(...).
    /// </summary>
    public static class NexusCSharpEval
    {
        private static object _evaluator;
        private static MethodInfo _evaluateMethod;
        private static MethodInfo _runMethod;
        private static MethodInfo _referenceAssemblyMethod;
        private static StringBuilder _captured = new StringBuilder();
        private static readonly object _lock = new object();

        public static string Run(Dictionary<string, string> parameters)
        {
            var code = parameters != null && parameters.TryGetValue("code", out var c) ? c : "";
            if (string.IsNullOrEmpty(code))
            {
                return JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = "code parameter is required",
                    example = "GameObject.Find(\"Cube\")?.name"
                });
            }

            lock (_lock)
            {
                try
                {
                    if (!EnsureInitialized(out var initError))
                    {
                        return JsonConvert.SerializeObject(new
                        {
                            success = false,
                            error = initError
                        });
                    }

                    _captured.Length = 0;
                    var oldOut = Console.Out;
                    Console.SetOut(new StringWriter(_captured));
                    try
                    {
                        // First try as an expression (Evaluate). Expressions must
                        // not end with ";". If the parse fails or returns a
                        // non-empty remainder, fall back to statement mode (Run).
                        bool isExpression = !code.TrimEnd().EndsWith(";");
                        if (isExpression && _evaluateMethod != null)
                        {
                            var args = new object[] { code, null, false };
                            object remainderObj = null;
                            try { remainderObj = _evaluateMethod.Invoke(_evaluator, args); }
                            catch (TargetInvocationException tie)
                            {
                                return Error(tie.InnerException ?? tie);
                            }

                            string remainder = remainderObj as string ?? "";
                            object result = args[1];
                            bool resultSet = args[2] is bool b && b;

                            if (string.IsNullOrEmpty(remainder))
                            {
                                return JsonConvert.SerializeObject(new
                                {
                                    success = true,
                                    output = _captured.ToString(),
                                    result = resultSet ? SafeStringify(result) : null,
                                    resultSet
                                });
                            }
                            // Parsed remainder — likely a statement. Fall through.
                        }

                        // Statement mode.
                        if (_runMethod == null)
                        {
                            return JsonConvert.SerializeObject(new
                            {
                                success = false,
                                error = "Evaluator.Run method not found on this Mono.CSharp build"
                            });
                        }

                        object runResult;
                        try { runResult = _runMethod.Invoke(_evaluator, new object[] { code }); }
                        catch (TargetInvocationException tie)
                        {
                            return Error(tie.InnerException ?? tie);
                        }

                        bool runOk = runResult is bool rb && rb;
                        return JsonConvert.SerializeObject(new
                        {
                            success = runOk,
                            output = _captured.ToString(),
                            result = (object)null
                        });
                    }
                    finally
                    {
                        Console.SetOut(oldOut);
                    }
                }
                catch (Exception e)
                {
                    return Error(e);
                }
            }
        }

        private static bool EnsureInitialized(out string error)
        {
            error = null;
            if (_evaluator != null) return true;

            Assembly mcs = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Mono.CSharp");
            if (mcs == null)
            {
                try { mcs = Assembly.Load("Mono.CSharp"); } catch { /* try alt below */ }
            }
            if (mcs == null)
            {
                error = "Mono.CSharp.dll is not loaded in this Unity build. " +
                        "Add an .asmdef reference to Mono.CSharp or use a Unity " +
                        "version that bundles it (most Unity LTS releases do).";
                return false;
            }

            Type settingsType = mcs.GetType("Mono.CSharp.CompilerSettings");
            Type printerType  = mcs.GetType("Mono.CSharp.ConsoleReportPrinter");
            Type reportType   = mcs.GetType("Mono.CSharp.Report");
            Type contextType  = mcs.GetType("Mono.CSharp.CompilerContext");
            Type evalType     = mcs.GetType("Mono.CSharp.Evaluator");

            if (settingsType == null || printerType == null || contextType == null || evalType == null)
            {
                error = "Mono.CSharp internal types missing " +
                        $"(settings={settingsType != null}, printer={printerType != null}, " +
                        $"context={contextType != null}, eval={evalType != null}).";
                return false;
            }

            object settings = Activator.CreateInstance(settingsType);
            object printer  = Activator.CreateInstance(printerType);

            // Try CompilerContext(CompilerSettings, ReportPrinter)
            object ctx = null;
            ConstructorInfo ctxCtor = contextType.GetConstructors()
                .FirstOrDefault(ci => ci.GetParameters().Length == 2);
            if (ctxCtor != null)
            {
                try { ctx = ctxCtor.Invoke(new object[] { settings, printer }); }
                catch { ctx = null; }
            }

            if (ctx == null)
            {
                error = "Could not construct Mono.CSharp.CompilerContext.";
                return false;
            }

            ConstructorInfo evalCtor = evalType.GetConstructor(new[] { contextType });
            if (evalCtor == null)
            {
                error = "Mono.CSharp.Evaluator(CompilerContext) constructor not found.";
                return false;
            }
            _evaluator = evalCtor.Invoke(new object[] { ctx });

            _evaluateMethod = evalType.GetMethod(
                "Evaluate",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(string), typeof(object).MakeByRefType(), typeof(bool).MakeByRefType() },
                null);

            _runMethod = evalType.GetMethod(
                "Run",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(string) },
                null);

            _referenceAssemblyMethod = evalType.GetMethod(
                "ReferenceAssembly",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(Assembly) },
                null);

            // Inject every already-loaded assembly so user code can reach
            // UnityEngine / UnityEditor / Newtonsoft.Json / the project's
            // own scripts without manual `using`.
            if (_referenceAssemblyMethod != null)
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        if (asm.IsDynamic) continue;
                        if (string.IsNullOrEmpty(asm.Location)) continue;
                        _referenceAssemblyMethod.Invoke(_evaluator, new object[] { asm });
                    }
                    catch { /* skip individual failures */ }
                }
            }

            // Pre-import common namespaces.
            if (_runMethod != null)
            {
                try
                {
                    _runMethod.Invoke(_evaluator, new object[]
                    {
                        "using System; " +
                        "using System.Collections.Generic; " +
                        "using System.Linq; " +
                        "using System.IO; " +
                        "using UnityEngine; " +
                        "using UnityEditor; " +
                        "using Newtonsoft.Json;"
                    });
                }
                catch { /* best-effort */ }
            }

            return true;
        }

        private static string Error(Exception e)
        {
            return JsonConvert.SerializeObject(new
            {
                success = false,
                error = e.Message,
                stackTrace = e.StackTrace
            });
        }

        private static object SafeStringify(object value)
        {
            if (value == null) return null;
            try
            {
                var t = value.GetType();
                if (t.IsPrimitive || value is string) return value;
                if (typeof(UnityEngine.Object).IsAssignableFrom(t)) return value.ToString();
                return JsonConvert.SerializeObject(value);
            }
            catch
            {
                return value?.ToString();
            }
        }
    }
}
