using System; 
using System.Web; 
using System.Web.Mvc; 
using System.IO; 
using System.Xml; 
using System.Text; 
using System.Collections.Generic; 
using System.Web.Script.Serialization; 
using System.Runtime.Serialization; 
using System.Reflection; 
using System.Reflection.Emit; 
 
namespace AleemBawany.ActionFilters 
{ 
  public class JsonPox : ActionFilterAttribute 
  { 
    private String[] _actionParams; 
 
    // for deserialization 
    public JsonPox(params String[] parameters) 
    { 
      this._actionParams = parameters; 
    } 
 
    // SERIALIZE 
    public override void OnActionExecuted(ActionExecutedContext filterContext) 
    { 
      if (!(filterContext.Result is ViewResult)) return; 
 
      // SETUP 
      UTF8Encoding utf8 = new UTF8Encoding(false); 
      HttpRequestBase request = filterContext.RequestContext.HttpContext.Request; 
      String contentType = request.ContentType ?? string.Empty; 
      ViewResult view = (ViewResult)(filterContext.Result); 
      var data = view.ViewData.Model; 
 
      // JSON 
      if (contentType.Contains("application/json") || request.IsAjaxRequest()) 
      { 
        using (var stream = new MemoryStream()) 
        { 
          JavaScriptSerializer js = new JavaScriptSerializer(); 
 
          String content = js.Serialize(data); 
          filterContext.Result = new ContentResult 
          { 
            ContentType = "application/json", 
            Content = content, 
            ContentEncoding = utf8 
          }; 
        } 
 
      } 
 
      // POX 
      else if (contentType.Contains("text/xml")) 
      { 
        // MemoryStream to encapsulate as UTF-8 (default UTF-16) 
        // http://stackoverflow.com/questions/427725/ 
        // 
        // MemoryStream also used for atomicity but not here 
        // http://stackoverflow.com/questions/486843/ 
        using (MemoryStream stream = new MemoryStream(500)) 
        { 
          using (var xmlWriter = 
            XmlTextWriter.Create(stream, 
              new XmlWriterSettings() 
              { 
                OmitXmlDeclaration = true, 
                Encoding = utf8, 
                Indent = true 
              })) 
          { 
 
            new DataContractSerializer( 
              data.GetType(), 
              null, // knownTypes 
              65536, // maxItemsInObjectGraph 
              false, // ignoreExtensionDataObject 
              true, // preserveObjectReference - overcomes cyclical reference issues 
              null // dataContractSurrogate 
              ).WriteObject(stream, data); 
          } 
 
          filterContext.Result = new ContentResult 
          { 
            ContentType = "text/xml", 
            Content = utf8.GetString(stream.ToArray()), 
            ContentEncoding = utf8 
          }; 
        } 
      } 
    } 
 
    // DESERIALIZE 
    public override void OnActionExecuting(ActionExecutingContext filterContext) 
    { 
 
      if (_actionParams == null || _actionParams.Length == 0) return; 
 
      HttpRequestBase request = filterContext.RequestContext.HttpContext.Request; 
      String contentType = request.ContentType ?? string.Empty; 
      Boolean isJson = contentType.Contains("application/json"); 
 
      if (!isJson) return; 
      //@@todo Deserialize POX 
 
      // JavascriptSerialier expects a single type to deserialize 
      // so if the response contains multiple disparate objects to deserialize 
      // we dynamically build a new wrapper class with fields representing those 
      // object types, deserialize and then unwrap 
      ParameterDescriptor[] paramDescriptors = 
          filterContext.ActionDescriptor.GetParameters(); 
      Boolean complexType = paramDescriptors.Length > 1; 
 
      Type wrapperClass; 
      if (complexType) 
      { 
        Dictionary parameterInfo = new Dictionary(); 
        foreach (ParameterDescriptor p in paramDescriptors) 
        { 
          parameterInfo.Add(p.ParameterName, p.ParameterType); 
        } 
        wrapperClass = BuildWrapperClass(parameterInfo); 
      } 
      else 
      { 
        wrapperClass = paramDescriptors[0].ParameterType; 
      } 
 
      String json; 
      using (var sr = new StreamReader(request.InputStream)) 
      { 
        json = sr.ReadToEnd(); 
      } 
 
      // then deserialize json as instance of dynamically created wrapper class 
      JavaScriptSerializer serializer = new JavaScriptSerializer(); 
      var result = typeof(JavaScriptSerializer) 
              .GetMethod("Deserialize") 
              .MakeGenericMethod(wrapperClass) 
              .Invoke(serializer, new object[] { json }); 
 
      // then get fields from wrapper class assign the values back to the action params 
      if (complexType) 
      { 
        for (Int32 i = 0; i < paramDescriptors.Length; i++) 
        { 
          ParameterDescriptor pd = paramDescriptors[i]; 
          filterContext.ActionParameters[pd.ParameterName] = 
              wrapperClass.GetField(pd.ParameterName).GetValue(result); 
 
        } 
      } 
      else 
      { 
        ParameterDescriptor pd = paramDescriptors[0]; 
        filterContext.ActionParameters[pd.ParameterName] = result; 
      } 
    } 
 
    private Type BuildWrapperClass(Dictionary parameterInfo) 
    { 
      AssemblyName assemblyName = new AssemblyName(); 
      assemblyName.Name = "DynamicAssembly"; 
      AppDomain appDomain = AppDomain.CurrentDomain; 
      AssemblyBuilder assemblyBuilder = 
          appDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run); 
      ModuleBuilder moduleBuilder = 
          assemblyBuilder.DefineDynamicModule("DynamicModule"); 
      TypeBuilder typeBuilder = 
          moduleBuilder.DefineType("DynamicClass", 
          TypeAttributes.Public | TypeAttributes.Class); 
 
      foreach (KeyValuePair entry in parameterInfo) 
      { 
        String paramName = entry.Key; 
        Type paramType = entry.Value; 
        FieldBuilder field = typeBuilder.DefineField(paramName, 
                    paramType, FieldAttributes.Public); 
      } 
 
      Type generatedType = typeBuilder.CreateType(); 
      // object generatedObject = Activator.CreateInstance(generatedType); 
 
      return generatedType; 
    } 
 
  } 
}