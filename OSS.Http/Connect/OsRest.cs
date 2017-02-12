﻿#region Copyright (C) 2016 Kevin (OSS开源系列) 公众号：osscoder

/***************************************************************************
*　　	文件功能描述：Http请求 == 主请求实体
*
*　　	创建人： Kevin
*       创建人Email：1985088337@qq.com
*       
*****************************************************************************/

#endregion
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OSS.Http.Models;

namespace OSS.Http.Connect
{
    /// <summary>
    ///  请求基类
    /// </summary>
    public class OsRest:HttpClient
    {
        private const string _lineBreak = "\r\n";
        public Encoding Encoding { get; set; } = Encoding.UTF8;
        //private static readonly Dictionary<string,Action<HttpContentHeaders,string>> _notCanAddContentHeaderDics
        //    =new Dictionary<string, Action<HttpContentHeaders, string>>();

        public OsRest():this(new HttpClientHandler(),true) 
        {
        }
        public OsRest(HttpMessageHandler handler) 
            : this(handler,true)
        {
        }
        public OsRest(HttpMessageHandler handler,bool disposeHandler) : base(handler, disposeHandler)
        {
        }

        /// <summary>
        ///  执行请求方法
        /// </summary>
        /// <param name="request"></param>
        /// <param name="completionOption"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task<HttpResponseMessage> RestSend(OsHttpRequest request,
            HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead,
            CancellationToken cancellationToken = new CancellationToken())
        {
            var reqMsg = ConfigureReqMsg(request);

            if (request.TimeOutMilSeconds > 0)
                this.Timeout = TimeSpan.FromMilliseconds(request.TimeOutMilSeconds);

            return SendAsync(reqMsg, completionOption, cancellationToken);
        }


        #region  配置 ReqMsg信息

        /// <summary>
        /// 配置请求
        /// </summary>
        /// <returns></returns>
        public HttpRequestMessage ConfigureReqMsg(OsHttpRequest request)
        {
            var reqMsg = new HttpRequestMessage();
            
            reqMsg.RequestUri = ConfigReqUriMsg(request);
            reqMsg.Method = new HttpMethod(request.HttpMothed.ToString());

            ConfigReqContent(reqMsg,request);   //  配置内容
       
            request.RequestSet?.Invoke(reqMsg);

            return reqMsg;
        }

        /// <summary>
        /// 创建请求的 Url 对象
        /// </summary>
        /// <param name="request">请求的对象实体</param>
        /// <returns>返回的 System.Uri 对象</returns>
        private Uri ConfigReqUriMsg(OsHttpRequest request)
        {
            if (string.IsNullOrEmpty(request.AddressUrl))
            {
                throw new ArgumentNullException(nameof(request), "无效的请求，请求对象中的 UrlAddress 不能为空");
            }
            string urlAddress = request.AddressUrl;

            var queryPara = GetReqParameters(request, ParameterType.Query);
            urlAddress = queryPara.Aggregate(urlAddress, (current, p) => string.Concat(current, current.IndexOf("?", StringComparison.CurrentCultureIgnoreCase) < 0 ? "?" : "&", p.ToString()));
            
            return new Uri(urlAddress);
        }

        /// <summary>
        ///  配置使用的cotent
        /// </summary>
        /// <param name="reqMsg"></param>
        /// <param name="req"></param>
        /// <returns></returns>
        private void ConfigReqContent(HttpRequestMessage reqMsg, OsHttpRequest req)
        {
            var contentStream = new MemoryStream();

            reqMsg.Content = new StreamContent(contentStream);
            reqMsg.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

            if (req.HasFile)
            {
                string boundary = GetBoundary();
                reqMsg.Content.Headers.ContentLength = WriteMultipartFormData(contentStream, req, boundary);
            }
            else
            {
                string data = GetNormalFormData(req);
                if (!string.IsNullOrEmpty(data))
                {
                    var bytes = Encoding.GetBytes(data);
                    int length = bytes.Length;
                    contentStream.Write(bytes, 0, length);
                    reqMsg.Content.Headers.ContentLength = length;
                }
            }
        }


        ///// <summary>
        ///// 准备请求的 头部 数据
        ///// </summary>
        ///// <param name="contentHeader"></param>
        ///// <param name="request"></param>
        //protected virtual void PrepareHeaders(HttpContentHeaders contentHeader, OsHttpRequest request)
        //{
        //    var headerParas = GetReqParameters(request, ParameterType.Header);
        //    foreach (var header in headerParas)
        //    {
        //        if (_notCanAddContentHeaderDics.ContainsKey(header.Name))
        //        {
        //            _notCanAddContentHeaderDics[header.Name].Invoke(contentHeader, header.Value.ToString());
        //        }
        //        else
        //        {
        //            contentHeader.Add(header.Name, header.Value.ToString());
        //        }

        //    }
        //}
        #endregion


        #region   请求数据的 内容 处理

        #region 处理带文件上传的数据处理
        /// <summary>
        /// 创建 请求 分割界限
        /// </summary>
        /// <returns></returns>
        private static string GetBoundary()
        {
            string pattern = "abcdefghijklmnopqrstuvwxyz0123456789";
            StringBuilder boundaryBuilder = new StringBuilder();
            Random rnd = new Random();
            for (int i = 0; i < 10; i++)
            {
                var index = rnd.Next(pattern.Length);
                boundaryBuilder.Append(pattern[index]);
            }
            return string.Format("-----------------------------{0}", boundaryBuilder.ToString());
        }

        ///// <summary>
        ///// 返回含文件请求的ContentType
        ///// </summary>
        ///// <param name="boundary"></param>
        ///// <returns>返回  WebRequest 的 ContenType 信息</returns>
        //private static string GetMultipartFormContentType(string boundary)
        //{
        //    return string.Format("multipart/form-data; boundary={0}", boundary);
        //}

        /// <summary>
        /// 写入 Form 的内容值 【 非文件参数 + 文件头 + 文件参数（内部完成） + 请求结束符 】
        /// </summary> 
        /// <param name="webRequestStream"></param>
        /// <param name="request"></param>
        private int WriteMultipartFormData(Stream webRequestStream, OsHttpRequest request, string boundary)
        {
            var formparas =GetReqParameters(request,ParameterType.Form);

            int contentLength = formparas.Sum(param => WriteStringTo(webRequestStream, GetMultipartFormData(param, boundary)));

            foreach (var file in request.FileParameterList)
            {
                //文件头
                contentLength+= WriteStringTo(webRequestStream, GetMultipartFileHeader(file, boundary));
                //文件内容
                contentLength += file.Writer(webRequestStream);
                //文件结尾
                contentLength += WriteStringTo(webRequestStream, _lineBreak);
            }
            //写入整个请求的底部信息
            contentLength += WriteStringTo(webRequestStream, GetMultipartFooter(boundary));
            return contentLength;
        }

        /// <summary>
        /// 写入 Form 的内容值（非文件参数）
        /// </summary>
        /// <param name="param"></param>
        /// <param name="boundary"></param>
        /// <returns></returns>
        private static string GetMultipartFormData(Parameter param, string boundary)
        {
            return string.Format("--{0}{3}Content-Disposition: form-data; name=\"{1}\"{3}{3}{2}{3}",
                boundary, param.Name, param.Value, _lineBreak);
        }

        /// <summary>
        /// 写入 Form 的内容值（文件头）
        /// </summary>
        /// <param name="file"></param>
        /// <param name="boundary"></param>
        /// <returns></returns>
        private static string GetMultipartFileHeader(FileParameter file, string boundary)
        {
            return string.Format("--{0}{4}Content-Disposition: form-data; name=\"{1}\"; filename=\"{2}\"{4}Content-Type: {3}{4}{4}",
                boundary, file.Name, file.FileName, file.ContentType ?? "application/octet-stream", _lineBreak);
        }

        /// <summary>
        /// 写入 Form 的内容值  （请求结束符）
        /// </summary>
        /// <param name="boundary"></param>
        /// <returns></returns>
        private static string GetMultipartFooter(string boundary)
        {
            return string.Format("--{0}--{1}", boundary, _lineBreak);
        }
        #endregion

        #region 不包含文件的数据处理（正常 get/post 请求）
        /// <summary>
        /// 写入请求的内容信息 （非文件上传请求）
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        private static string GetNormalFormData(OsHttpRequest request)
        {
            var formParas = GetReqParameters(request, ParameterType.Form);
            var formstring = new StringBuilder();
            foreach (var p in formParas)
            {
                if (formstring.Length > 1)
                    formstring.Append("&");
                formstring.AppendFormat("{0}={1}", p.Name, p.Value);
            }
            if (!string.IsNullOrEmpty(request.CustomBody))
            {
                if (formstring.Length > 1)
                    formstring.Append("&");
                formstring.Append(request.CustomBody);
            }
            return formstring.ToString();
        }
        #endregion

        #endregion


        #region 请求辅助方法
        /// <summary>
        /// 写入数据方法（将数据写入  webrequest）
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="toWrite"></param>
        /// <returns>写入的字节数量</returns>
        private  int WriteStringTo(Stream stream, string toWrite)
        {
            var bytes = Encoding.GetBytes(toWrite);
            stream.Write(bytes, 0, bytes.Length);
            return bytes.Length;
        }
        //protected static void AddStaticHeaderDictionary()
        //{
        //    _notCanAddContentHeaderDics.Add("Accept", (r, v) => r.Accept = v);
        //    _notCanAddContentHeaderDics.Add("Content-Type", (r, v) => r.ContentType = v);

        //    _notCanAddContentHeaderDics.Add("Date", (r, v) =>
        //    {
        //        DateTime parsed;
        //        if (DateTime.TryParse(v, out parsed))
        //        {
        //            r.Date = parsed;
        //        }
        //    });
        //    _notCanAddContentHeaderDics.Add("Host", (r, v) => r.Host = v);
        //    _notCanAddContentHeaderDics.Add("Connection", (r, v) => r.Connection = v);
        //    _notCanAddContentHeaderDics.Add("Content-Length", (r, v) => r.ContentLength = Convert.ToInt64(v));
        //    _notCanAddContentHeaderDics.Add("Expect", (r, v) => r.Expect = v);
        //    _notCanAddContentHeaderDics.Add("If-Modified-Since", (r, v) => r. = Convert.ToDateTime(v));
        //    _notCanAddContentHeaderDics.Add("Referer", (r, v) => r.Referer = v);
        //    _notCanAddContentHeaderDics.Add("Transfer-Encoding", (r, v) => { r.TransferEncoding = v; r.SendChunked = true; });
        //    _notCanAddContentHeaderDics.Add("User-Agent", (r, v) => r.UserAgent = v);
        //}

        private static List<Parameter> GetReqParameters(OsHttpRequest request,ParameterType type)
        {
            return request.Parameters.Where(p => p.Type == type).ToList() ?? new List<Parameter>();
        }
        #endregion
    }
}
