﻿using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Azpe.Viewer
{
	public enum MediaTypes	: byte { Image,		Video,		VideoThumb, PageThumb	}
	public enum Statuses	: byte { Download,	Complete,	Error }

	public class MediaInfo : IDisposable
	{
		private FrmViewer	m_parent;
		private int			m_index;

		private WebClient	m_web;
		private string		m_temp;
		private float		m_speed2;
		private long		m_down2;
		private DateTime	m_date;

		public MediaTypes	MediaType	{ get; private set; }
		public string		OrigUrl		{ get; private set; }
		public string		Url			{ get; private set; }
		public string		CachePath	{ get; private set; }
		public Image		Image		{ get; private set; }
		public float		Progress	{ get; private set; }
		public float		Speed		{ get; private set; }
		public Statuses		Status		{ get; private set; }

		~MediaInfo()
		{
			this.Dispose(false);
		}

		public void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private bool m_disposed = false;
		protected virtual void Dispose(bool disposing)
		{
			if (!this.m_disposed)
			{
				this.m_disposed = true;

				if (this.Image != null)
				{
					this.Image.Dispose();
					this.Image = null;
				}

				if (this.m_web != null)
				{
					if (this.m_web.IsBusy)
					{
						this.m_web.CancelAsync();
						while (this.m_web.IsBusy)
							Thread.Sleep(250);
					}
					this.m_web.Dispose();
				}
			}
		}

		public MediaInfo(string url, int index)
		{
			MediaTypes	mediaType;

			this.m_index	= index;
			this.OrigUrl	= url;
			this.Url		= MediaInfo.FixUrl(url, out mediaType);
			this.MediaType	= mediaType;
		}

		public void SetParent(FrmViewer parent)
		{
			this.m_parent = parent;
		}

		public void RefreshItem()
		{
			if (!this.m_disposed && this.m_parent.CurrentIndex == this.m_index)
				this.m_parent.RefreshItem();
		}

		public void StartDownload()
		{
			this.Status = Statuses.Download;

			try
			{
				File.Delete(this.m_temp);
			}
			catch
			{ }

			new Task(this.Download).Start();

			this.RefreshItem();
		}

		public void CreateWebClient()
		{
			if (this.m_web == null)
			{
				this.m_web = new WebClient();
				this.m_web.DownloadFileCompleted	+= DownloadFileCompleted;
				this.m_web.DownloadProgressChanged	+= DownloadProgressChanged;
			}
		}

		private void DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
		{
            DateTime dt = DateTime.UtcNow;
            TimeSpan ts = dt - this.m_date;

            if (this.m_down2 == 0)
            {
                this.Speed		= e.BytesReceived / (float)ts.TotalSeconds;

                this.m_date		= dt;
                this.m_down2	= e.BytesReceived;
            }
            else if (ts.TotalMilliseconds > 250)
            {
                this.m_speed2	= (e.BytesReceived - this.m_down2) / (float)ts.TotalSeconds;
                this.Speed		= (this.Speed * 3 + this.m_speed2) / 4;

                this.m_date		= dt;
                this.m_down2	= e.BytesReceived;
            }

			this.Progress = e.TotalBytesToReceive == 0 ? 0 : e.BytesReceived / (float)e.TotalBytesToReceive;

			this.RefreshItem();
		}

		private void DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
		{
			if (e.Error != null)
			{
				Cache.Remove((string)e.UserState, null);

				this.Status = Statuses.Error;
				this.RefreshItem();
			}
		}

		private static RegexOptions regRules = RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled;
		private static Regex regYoutube	= new Regex(@"^(?:https?://)?(?:(?:(?:www\.)?youtube\.com/(?:v/)?watch[\?#]?.*v=)|(?:youtu\.be/))([A-Za-z0-9_\-]+).*$", regRules);
		private static Regex regVine	= new Regex(@"^https?://vine.co/v/[a-zA-Z0-9]+$", regRules);

		private static string FixUrl(string url, out MediaTypes mediaType)
		{
			mediaType = MediaTypes.Image;

			if (url.Contains("tweet_video_thumb"))
			{
				mediaType = MediaTypes.Video;
                url = Path.ChangeExtension(url.Replace("tweet_video_thumb", "tweet_video"), ".mp4");
				return url;
			}
            
            if (url.Contains("ext_tw_video_thumb"))
            {
                mediaType = MediaTypes.VideoThumb;
                return url;
            }

			if (url.Contains("ext_tw_video"))
			{
				mediaType = MediaTypes.VideoThumb;
				return url.EndsWith(":orig") ? url : url + ":orig";
			}

			if (url.Contains("pbs.twimg.com"))
				return url.EndsWith(":orig") ? url : url + ":orig";

			if (url.Contains("p.twipple.jp/"))
				return url.Replace("p.twipple.jp/", "p.twpl.jp/show/orig/");

			if (url.Contains("twitrpix.com/"))
				return url.Replace("twitrpix.com/", "img.twitrpix.com/");

			if (url.Contains("img.ly/"))
				return url.Replace("img.ly/", "img.ly/show/full/");

			if (url.Contains("lockerz.com/s/"))
				return url.Replace("lockerz.com/s/", "api.plixi.com/api/tpapi.svc/imagefromurl?url=http://plixi.com/p/") + "&size=big";

			if (url.Contains("pikchur.com/"))
				return url.Replace("pikchur.com/", "img.pikchur.com/pic_") + "_l.jpg";

			if (url.Contains("puu.sh/"))
				return url;

			if (url.Contains("pckles.com"))
				return url;

			if (url.Contains("twitpic.com"))
				return url.Replace("twitpic.com", "www.twitpic.com/show/full/");

			if (url.EndsWith(".png") || url.EndsWith(".jpg") || url.EndsWith(".gif"))
				return url;
			
			Match m;
			
			m = regYoutube.Match(url);
			if (m.Success)
			{
				mediaType = MediaTypes.VideoThumb;
				return string.Format("http://img.youtube.com/vi/{0}/sddefault.jpg", m.Groups[1].Value);
			}

			m = regVine.Match(url);
			if (m.Success)
			{
				mediaType = MediaTypes.Video;
				return url;
			}

			mediaType = MediaTypes.PageThumb;
			return url;
		}

		private void Download()
		{
			int retry = 3;
			do
			{
                if (this.MediaType == MediaTypes.PageThumb)
                    this.GetPageThumb();

                this.Download(this.MediaType == MediaTypes.Video);
			} while (--retry > 0 && this.Status == Statuses.Error);

			if (this.m_web != null && this.Status == Statuses.Complete)
				this.m_web.Dispose();

			this.RefreshItem();
		}

		private static char[] InvalidChars = Path.GetInvalidFileNameChars();
		private static Stream GetCache(string url, out string cacheName, out string cachePath)
		{
			cachePath = Cache.GetCachePath(url, out cacheName);

			if (File.Exists(cachePath))
				return new FileStream(cachePath, FileMode.Open, FileAccess.Read);
			else
				return null;
		}

		private void Download(bool isVideo)
		{
			try
			{
                if (isVideo)
                    this.GetVideoUrl();

				string cacheName, cachePath;
				Stream file = MediaInfo.GetCache(this.OrigUrl, out cacheName, out cachePath);

				this.CachePath = cachePath;
                
				if (file != null )
				{
                    // 비디오 다운로드 부분
                    if (isVideo)
                        file.Dispose();
                    else
					    using (file)
						    this.Image = Image.FromStream(file);
				}
				else
				{
					if (!Directory.Exists(Cache.CachePath))
						Directory.CreateDirectory(Cache.CachePath);

					this.m_temp = cachePath + ".tmp";

					this.CreateWebClient();

					this.m_web.Headers.Add(HttpRequestHeader.UserAgent, Program.UserAgent);

					this.m_date = DateTime.UtcNow;
					this.m_web.DownloadFileAsync(new Uri(this.Url), this.m_temp, this.Url);

					while (this.m_web.IsBusy)
						Thread.Sleep(100);

                    var ext = this.Url;
                    ext = ext.Substring(ext.IndexOf('.', ext.LastIndexOf('/') + 1));
                    if (ext.IndexOf('?') != -1) ext = ext.Substring(0, ext.IndexOf('?'));
                    if (ext.IndexOf(':') != -1) ext = ext.Substring(0, ext.IndexOf(':'));

                    cachePath = cachePath + ext;
                    this.CachePath = cachePath;

                    Cache.SetNewCachePath(cacheName, cacheName + ext);

					File.Move(this.m_temp, cachePath);

                    if (!isVideo)
                        using (file = new FileStream(cachePath, FileMode.Open, FileAccess.Read))
                            this.Image = Image.FromStream(file);
				}
				
				this.Status = Statuses.Complete;
			}
			catch
			{
				Cache.Remove(this.Url, null);
				this.Status = Statuses.Error;
			}
		}
		
		private static Regex regVineMp4 = new Regex("<video src=\"([^\"]+)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private void GetVideoUrl()
		{
			if (this.OrigUrl.Contains("vine.co/v/"))
			{
				string body;

				try
				{
					var req = HttpWebRequest.Create(this.Url) as HttpWebRequest;
					req.UserAgent = Program.UserAgent;

					using (var res = req.GetResponse())
					using (var red = new StreamReader(res.GetResponseStream(), Encoding.UTF8))
						body = red.ReadToEnd();
				}
				catch
				{
					this.Status = Statuses.Error;
					return;
				}

				var m = regVineMp4.Match(body);
				if (m.Success)
				{
					this.Url	= m.Groups[1].Value;
				}
				else
				{
					this.Status	= Statuses.Error;
				}
			}
		}

		private void GetPageThumb()
		{
			try
			{
				var req = HttpWebRequest.Create(this.Url) as HttpWebRequest;
				req.UserAgent = Program.UserAgent;
				req.Method = "HEAD";
				using (var res = req.GetResponse())
				{
					string type = res.Headers["content-type"];

					if (type.StartsWith("image/"))
						this.MediaType = MediaTypes.Image;

					res.Close();
				}

                return;
			}
			catch
			{
				this.MediaType = MediaTypes.PageThumb;
			}

			Image img = WebThumbnail.GetWebThumbnail(this.Url);

			if (img != null)
			{
				this.Image = img;
				this.Status = Statuses.Complete;
			}
			else
			{
				this.Status = Statuses.Error;
			}
		}
	}
}