using System;
using System.Web;
using System.Web.Mvc;
using System.IO;
using System.Xml;
using System.Text;
using System.Xml.Serialization;

namespace Testy.ActionFilters
{
    public class JsonPox : ActionFilterAttribute
    {
        private static UTF8Encoding UTF8 = new UTF8Encoding(false);

        public override void OnActionExecuted(ActionExecutedContext filterContext)
        {
            // setup the request, view and data
            HttpRequestBase request = filterContext.RequestContext.HttpContext.Request;
            ViewResult view = (ViewResult)(filterContext.Result);
            var data = view.ViewData.Model;

            String contentType = request.ContentType ?? string.Empty;

            // JSON
            if (contentType.Contains("application/json"))
            {
                filterContext.Result = new JsonResult
                {
                    Data = data
                };
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
                                Encoding = UTF8,
                                Indent = true
                            }))
                    {
                        new XmlSerializer(data.GetType()).Serialize(xmlWriter, data);
                    }

                    filterContext.Result = new ContentResult
                    {
                        ContentType = "text/xml",
                        Content = UTF8.GetString(stream.ToArray()),
                        ContentEncoding = UTF8
                    };
                }
            }
        }
    }
}
