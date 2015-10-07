using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ApiBuilder
{
    public class CodeUtility
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BuildControllerClasses(StreamWriter log, string folderPath, string projectNamespace, string dbContextClassName, IEnumerable<Type> types, IEnumerable<string> namespaceImports, string classSuffix)
        {
            Directory.CreateDirectory(folderPath);
            ConcurrentQueue<string> buffer = new ConcurrentQueue<string>();
            var hashSet = new HashSet<Type>(types);

            Parallel.ForEach(types, (entityType) =>
            {
                var mainTypeName = entityType.Name;
                var camelTypeName = GetCamelCaseName(mainTypeName);
                var modelClassName = camelTypeName + classSuffix;
                var propertyInfo = entityType.GetProperties()[0];
                var key = propertyInfo.Name;
                var keyType = propertyInfo.PropertyType;
                var nullableType = Nullable.GetUnderlyingType(keyType);
                var isNullable = nullableType != null;
                var keyTypeName = GetFriendlyTypeName(keyType.Name, isNullable);
                var controllerName = string.Format("{0}Controller", camelTypeName);

                var propertyMap = new Dictionary<string, string>();

                foreach (var property in entityType.GetProperties())
                {
                    if (!property.PropertyType.IsGenericType || isNullable)
                    {
                        var localType = isNullable ? nullableType : property.PropertyType;
                        var propertytypeName = GetFriendlyTypeName(localType.Name, isNullable);
                        if (!hashSet.Contains(property.PropertyType))
                        {
                            var propertyName = GetCamelCaseName(property.Name);

                            if (propertyName != "RowVersion")
                            {
                                propertyMap.Add(property.Name, propertyName);
                            }
                        }
                    }
                }

                using (StreamWriter writer = new StreamWriter(string.Format("{0}\\{1}.cs", folderPath, controllerName)))
                {
                    buffer.Enqueue(string.Format("[{0:yyyy-MM-dd HH:mm:ss}] : {1}.cs", DateTime.Now, controllerName));

                    foreach (var lineToWrite in namespaceImports)
                    {
                        writer.WriteLine(lineToWrite);
                    }

                    writer.WriteLine();
                    writer.WriteLine("namespace {0}.Controllers", projectNamespace);
                    writer.WriteLine("{");
                    writer.WriteLine("    public class {0} : ApiController", controllerName);
                    writer.WriteLine("    {");
                    writer.WriteLine("        private readonly DbContext _db = new {0}();", dbContextClassName);
                    writer.WriteLine();

                    writer.WriteLine("        private readonly Func<{0}, {1}> _toBusinessModel = x =>", mainTypeName, modelClassName);
                    writer.WriteLine("           new {0}", modelClassName);
                    writer.WriteLine("           {");

                    foreach (var property in propertyMap)
                    {
                        writer.WriteLine("               {0} = x.{1},", property.Value, property.Key);
                    }

                    writer.WriteLine("           };");
                    writer.WriteLine();

                    writer.WriteLine("        private readonly Func<{0}, {1}> _toDatabaseModel = x =>", modelClassName, mainTypeName);
                    writer.WriteLine("           new {0}", mainTypeName);
                    writer.WriteLine("           {");

                    foreach (var property in propertyMap)
                    {
                        writer.WriteLine("               {0} = x.{1},", property.Key, property.Value);
                    }

                    writer.WriteLine("           };");
                    writer.WriteLine();

                    // GetAll
                    writer.WriteLine("        // GET api/{0}", camelTypeName);
                    writer.WriteLine("        public IQueryable<{0}> Get{1}s()", modelClassName, camelTypeName);
                    writer.WriteLine("        {");
                    writer.WriteLine("            var modelList = new List<{0}>();", modelClassName);
                    writer.WriteLine();
                    writer.WriteLine("            foreach (var obj in this._db.Set<{0}>())", mainTypeName);
                    writer.WriteLine("            {");
                    writer.WriteLine("                modelList.Add(this._toBusinessModel(obj));");
                    writer.WriteLine("            }");
                    writer.WriteLine();
                    writer.WriteLine("            return modelList.AsQueryable();");
                    writer.WriteLine("        }");
                    writer.WriteLine();

                    // Get
                    writer.WriteLine("        // GET api/{0}/5", camelTypeName);
                    writer.WriteLine("        [ResponseType(typeof({0}))]", modelClassName);
                    writer.WriteLine("        public IHttpActionResult Get{0}({1} id)", camelTypeName, keyTypeName);
                    writer.WriteLine("        {");
                    writer.WriteLine("            {0} dataModel = this._db.Set<{0}>().Find(id);", mainTypeName);
                    writer.WriteLine();
                    writer.WriteLine("            if (dataModel == null)");
                    writer.WriteLine("            {");
                    writer.WriteLine("                return NotFound();");
                    writer.WriteLine("            }");
                    writer.WriteLine();
                    writer.WriteLine("            return Ok(this._toBusinessModel(dataModel));");
                    writer.WriteLine("        }");
                    writer.WriteLine();

                    // Put
                    writer.WriteLine("        // PUT api/{0}/5", camelTypeName);
                    writer.WriteLine("        public IHttpActionResult Put{0}({2} id, {1} model)", camelTypeName, modelClassName, keyTypeName);
                    writer.WriteLine("        {");
                    writer.WriteLine("            if (!ModelState.IsValid)");
                    writer.WriteLine("            {");
                    writer.WriteLine("                return BadRequest(ModelState);");
                    writer.WriteLine("            }");
                    writer.WriteLine();
                    writer.WriteLine("            if (id != model.{0})", GetCamelCaseName(key));
                    writer.WriteLine("            {");
                    writer.WriteLine("                return BadRequest();");
                    writer.WriteLine("            }");
                    writer.WriteLine();
                    writer.WriteLine("            this._db.Entry(this._toDatabaseModel(model)).State = EntityState.Modified;");
                    writer.WriteLine();
                    writer.WriteLine("            try");
                    writer.WriteLine("            {");
                    writer.WriteLine("                this._db.SaveChanges();");
                    writer.WriteLine("            }");
                    writer.WriteLine("            catch (DbUpdateConcurrencyException)");
                    writer.WriteLine("            {");
                    writer.WriteLine("                if (!{0}Exists(id))", camelTypeName);
                    writer.WriteLine("                {");
                    writer.WriteLine("                    return NotFound();");
                    writer.WriteLine("                }");
                    writer.WriteLine("                else");
                    writer.WriteLine("                {");
                    writer.WriteLine("                    throw;");
                    writer.WriteLine("                }");
                    writer.WriteLine("            }");
                    writer.WriteLine();
                    writer.WriteLine("            return StatusCode(HttpStatusCode.NoContent);");
                    writer.WriteLine("        }");
                    writer.WriteLine();

                    // Post
                    writer.WriteLine("        // POST api/{0}", camelTypeName);
                    writer.WriteLine("        [ResponseType(typeof({0}))]", modelClassName);
                    writer.WriteLine("        public IHttpActionResult Post{0}({1} model)", camelTypeName, modelClassName);
                    writer.WriteLine("        {");
                    writer.WriteLine("            if (!ModelState.IsValid)");
                    writer.WriteLine("            {");
                    writer.WriteLine("                return BadRequest(ModelState);");
                    writer.WriteLine("            }");
                    writer.WriteLine();
                    writer.WriteLine("            var dataModel = this._toDatabaseModel(model);");
                    writer.WriteLine();
                    writer.WriteLine("            this._db.Set<{0}>().Add(dataModel);", mainTypeName);
                    writer.WriteLine("            this._db.SaveChanges();");
                    writer.WriteLine();
                    writer.WriteLine("            return CreatedAtRoute(\"DefaultApi\", new { id = dataModel." + key + " }, model);");
                    writer.WriteLine("        }");
                    writer.WriteLine();

                    // Delete
                    writer.WriteLine("        // DELETE api/{0}/5", camelTypeName);
                    writer.WriteLine("        [ResponseType(typeof({0}))]", modelClassName);
                    writer.WriteLine("        public IHttpActionResult Delete{0}({1} id)", camelTypeName, keyTypeName);
                    writer.WriteLine("        {");
                    writer.WriteLine("            {0} dataModel = this._db.Set<{0}>().Find(id);", mainTypeName);
                    writer.WriteLine();
                    writer.WriteLine("            if (dataModel == null)");
                    writer.WriteLine("            {");
                    writer.WriteLine("                return NotFound();");
                    writer.WriteLine("            }");
                    writer.WriteLine();
                    writer.WriteLine("            this._db.Set<{0}>().Remove(dataModel);", mainTypeName);
                    writer.WriteLine("            this._db.SaveChanges();");
                    writer.WriteLine();
                    writer.WriteLine("            return Ok(this._toBusinessModel(dataModel));");
                    writer.WriteLine("        }");
                    writer.WriteLine();

                    // Dispose
                    writer.WriteLine("        protected override void Dispose(bool disposing)");
                    writer.WriteLine("        {");
                    writer.WriteLine("            if (disposing)");
                    writer.WriteLine("            {");
                    writer.WriteLine("                this._db.Dispose();");
                    writer.WriteLine("            }");
                    writer.WriteLine("            base.Dispose(disposing);");
                    writer.WriteLine("        }");
                    writer.WriteLine();

                    // Exists
                    writer.WriteLine("        private bool {0}Exists({1} id)", camelTypeName, keyTypeName);
                    writer.WriteLine("        {");
                    writer.WriteLine("            return this._db.Set<{0}>().Count(e => e.{1} == id) > 0;", mainTypeName, key);
                    writer.WriteLine("        }");
                    writer.WriteLine("    }");
                    writer.Write("}");
                }
            });

            log.AutoFlush = false;
            string line = null;
            while (buffer.TryDequeue(out line))
            {
                log.WriteLine(line);
            }
            log.Flush();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BuildModelClasses(StreamWriter log, IEnumerable<Type> types, string folderPath, string modelNamespace, string classSuffix)
        {
            var hashKeys = new HashSet<string>();
            Directory.CreateDirectory(folderPath);

            Parallel.ForEach(GetFilteredTypes(types), x =>
            {
                hashKeys.Add(x.Name);
            });

            ConcurrentQueue<string> buffer = new ConcurrentQueue<string>();

            Parallel.ForEach(GetFilteredTypes(types), type =>
            {
                try
                {
                    if (!type.IsGenericType && !type.Name.Contains("<"))
                    {
                        var typeName = type.Name;
                        typeName = GetCamelCaseName(typeName) + classSuffix;
                        buffer.Enqueue(string.Format("[{0:yyyy-MM-dd HH:mm:ss}] : {1}.cs", DateTime.Now, typeName));

                        using (StreamWriter writer = new StreamWriter(string.Format("{0}\\{1}.cs", folderPath, typeName)))
                        {
                            writer.WriteLine("using System;");

                            writer.WriteLine();
                            writer.WriteLine("namespace " + modelNamespace);
                            writer.WriteLine("{");
                            writer.WriteLine("    public class {0}", typeName);
                            writer.WriteLine("    {");

                            foreach (var property in type.GetProperties())
                            {
                                var nullableType = Nullable.GetUnderlyingType(property.PropertyType);
                                var isNullable = nullableType != null;

                                if (!property.PropertyType.IsGenericType || isNullable)
                                {
                                    var localType = isNullable ? nullableType : property.PropertyType;
                                    var propertytypeName = GetFriendlyTypeName(localType.Name, isNullable);
                                    if (!hashKeys.Contains(propertytypeName))
                                    {
                                        var propertyName = GetCamelCaseName(property.Name);

                                        if (propertyName != "RowVersion")
                                        {
                                            writer.WriteLine();
                                            writer.WriteLine("        // " + property.Name);

                                            writer.WriteLine(string.Format("        public {0} {1}", propertytypeName, GetCamelCaseName(property.Name)) + " { get; set; }");
                                        }
                                    }
                                }
                            }

                            writer.WriteLine("    }");
                            writer.Write("}");
                        }
                    }
                }
                catch
                {
                }
            });

            log.AutoFlush = false;
            string line = null;
            while (buffer.TryDequeue(out line))
            {
                log.WriteLine(line);
            }
            log.Flush();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetCamelCaseName(string name)
        {
            var camelCaseName = string.Empty;
            var tokens = name.Split(new char[] { '_' }, StringSplitOptions.RemoveEmptyEntries);

            for (var x = 0; x < tokens.Length; x++)
            {
                var token = tokens[x];

                if (x != 0 || token.Length > 1)
                {
                    token = token.Substring(0, 1).ToUpper() + (token.Length > 1 ? token.Substring(1, token.Length - 1) : string.Empty);
                    camelCaseName += token;
                }
            }

            return camelCaseName;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetDisplayName(string name)
        {
            var displayName = string.Empty;
            var tokens = name.ToCharArray();
            var hashKeys = new HashSet<char>("ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray());

            for (var x = 0; x < tokens.Length; x++)
            {
                var token = tokens[x];
                displayName += (hashKeys.Contains(token) ? " " : string.Empty) + token;
            }

            displayName = (displayName.EndsWith(" Id") && displayName.Length > 3 ? displayName.Substring(0, displayName.Length - 3) : displayName).Trim();

            return displayName;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<Type> GetFilteredTypes(IEnumerable<Type> typeList)
        {
            foreach (var type in typeList)
            {
                if (!type.IsGenericType && !type.Name.Contains("<") && !type.Name.Contains("=") && (type.BaseType == null || (type.BaseType != null && type.BaseType.Name != "DbContext")))
                {
                    yield return type;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetFriendlyTypeName(string typeName, bool isNullable)
        {
            switch (typeName.ToLower())
            {
                case "string":
                    typeName = "string";
                    break;

                case "int32":
                    typeName = "int";
                    break;

                case "int64":
                    typeName = "long";
                    break;

                case "double":
                    typeName = "double";
                    break;

                case "decimal":
                    typeName = "decimal";
                    break;

                case "boolean":
                    typeName = "bool";
                    break;

                case "byte":
                    typeName = "byte";
                    break;

                case "byte[]":
                    typeName = "byte[]";
                    break;
            }

            typeName = isNullable ? typeName + "?" : typeName;

            return typeName;
        }
    }
}