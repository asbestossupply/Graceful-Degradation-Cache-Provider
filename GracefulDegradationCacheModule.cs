using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Caching;
using System.Web;
using System.Collections;
using System.Net;
using System.Text;

namespace GroupCommerce.Web
{
    public class GracefulDegradationCacheModule : IHttpModule
    {
        public void Dispose()
        {
        }

        public void Init(HttpApplication context)
        {
            context.Error += new EventHandler(context_Error);
        }

        void context_Error(object sender, EventArgs e)
        {
            // Find the GracefulDegredationOutputCacheProvider
            foreach (var provider in OutputCache.Providers)
            {
                var inMemoryProvider = provider as GracefulDegradationOutputCacheProvider;
                if (inMemoryProvider == null)
                    continue;

                var cacheKey = HttpContext.Current.Items[GracefulDegradationOutputCacheProvider.KEY_CURRENT_REQUEST_CACHE_KEY] as string;
                // get cached object without respect for expiration date
                // if it's in there, just pull it out!
                var cacheItem = inMemoryProvider.Get(cacheKey, false) as IOutputCacheEntry;
                if (cacheItem != null)
                {
                    var context = HttpContext.Current;
                    var response = context.Response;
                    // clear response
                    response.Clear();

                    // we don't want anyone caching this old response, right?
                    response.CacheControl = "no-cache";

                    var responseBytes = GetResponseBytes(cacheItem, context);
                    // write out response body
                    // you can also append some content if you wish,
                    // perhaps a floating div that tells the user
                    // the page they're seeing is old
                    responseBytes.ForEach(x => 
                    {
                        response.OutputStream.Write(x, 0, x.Length);
                    });

                    // Don't want to show the error page (though you
                    // probably want to alert someone!)
                    context.ClearError();
                }
            }
        }

        /// <summary>
        /// System.Web.OutputCache has a private static method called Convert.  
        /// The code has been reworked but the cases covered are the same.
        /// </summary>
        /// <param name="oce"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        private List<byte[]> GetResponseBytes(IOutputCacheEntry oce, HttpContext context)
        {
            List<byte[]> buffers;
            if (oce.ResponseElements != null && oce.ResponseElements.Count > 0)
            {
                buffers = new List<byte[]>(oce.ResponseElements.Count);
                for (int index = 0; index < oce.ResponseElements.Count; ++index)
                {
                    ResponseElement responseElement = oce.ResponseElements[index];
                    if (responseElement is FileResponseElement)
                    {
                        var fileResponse = (FileResponseElement)responseElement;
                        byte[] fileBytes = new byte[fileResponse.Length];
                        using (var fileStream = System.IO.File.OpenRead(fileResponse.Path))
                        {
                            fileStream.Read(fileBytes, Convert.ToInt32(fileResponse.Offset), Convert.ToInt32(fileResponse.Length));
                        }
                        buffers.Add(fileBytes);
                    }
                    else if (responseElement is MemoryResponseElement)
                    {
                        var memoryResponse = (MemoryResponseElement)responseElement;
                        buffers.Add(memoryResponse.Buffer);
                    }
                    else if (responseElement is SubstitutionResponseElement)
                    {
                        var substitutionResponse = (SubstitutionResponseElement)responseElement;
                        string substitutionString = null;
                        try
                        {
                            substitutionString = substitutionResponse.Callback(context);
                        }
                        catch { }
                        if (substitutionString != null)
                        {
                            var bytes = Encoding.Default.GetBytes(substitutionString);
                            buffers.Add(bytes);
                        }
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                }
            }
            else
                buffers = new List<byte[]>(0);


            return buffers;
        }
    }
}
