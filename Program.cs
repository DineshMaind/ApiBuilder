using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace ApiBuilder
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            using (StreamWriter errorLog = new StreamWriter(string.Format("ErrorLog{0:yyyyMMddHHmmss}.txt", DateTime.Now)))
            {
                try
                {
                    Console.WriteLine("Creating controllers...");

                    var assemblyPath = string.Empty;
                    var projectNamespace = string.Empty;
                    var dataModelsNameSpace = string.Empty;
                    var dbContextClassName = string.Empty;

                    if (args.Length < 4)
                    {
                        assemblyPath = @"E:\MVCDemos\MyDemoWebApi\MyDemoWebApi.Entities\bin\Debug\MyDemoWebApi.Entities.dll";
                        projectNamespace = "MyDemoWebApi";
                        dataModelsNameSpace = "MyDemoWebApi.Entities";
                        dbContextClassName = "MyDemoWebApiEntities";
                    }
                    else
                    {
                        assemblyPath = args[0];
                        projectNamespace = args[1];
                        dataModelsNameSpace = args[2];
                        dbContextClassName = args[3];
                    }

                    var modelsNameSpace = projectNamespace + ".Models";
                    var modelsFolderPath = Path.Combine(Environment.CurrentDirectory, projectNamespace, "Models");
                    var controllersFolderPath = Path.Combine(Environment.CurrentDirectory, projectNamespace, "Controllers");

                    var namespaceImports = new string[]
                    {
                        string.Format("using {0};", dataModelsNameSpace),
                        string.Format("using {0};", modelsNameSpace),
                        "using System;",
                        "using System.Collections.Generic;",
                        "using System.Data.Entity;",
                        "using System.Data.Entity.Infrastructure;",
                        "using System.Linq;",
                        "using System.Net;",
                        "using System.Threading.Tasks;",
                        "using System.Web.Http;",
                        "using System.Web.Http.Description;"
                    };

                    Assembly assembly = Assembly.LoadFrom(assemblyPath);
                    var typeList = assembly.GetTypes();
                    var filteredTypes = CodeUtility.GetFilteredTypes(typeList);

                    var t1 = Task.Factory.StartNew(() =>
                    {
                        using (StreamWriter log = new StreamWriter(string.Format("ModelLog{0:yyyyMMddHHmmss}.txt", DateTime.Now)))
                        {
                            CodeUtility.BuildModelClasses(log, filteredTypes, modelsFolderPath, modelsNameSpace, "Model");
                        }
                    });

                    var t2 = Task.Factory.StartNew(() =>
                    {
                        using (StreamWriter log = new StreamWriter(string.Format("ControllerLog{0:yyyyMMddHHmmss}.txt", DateTime.Now)))
                        {
                            CodeUtility.BuildControllerClasses(log, controllersFolderPath, projectNamespace, dbContextClassName, filteredTypes, namespaceImports, "Model");
                        }
                    });

                    Task.WaitAll(t1, t2);

                    Console.WriteLine("Controllers creation completed...");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    errorLog.WriteLine("[{0:yyyy-MM-dd HH:mm:ss}] : {1}", DateTime.Now, ex);
                }
            }
        }
    }
}