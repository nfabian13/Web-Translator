﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Services.Translator
{
    public class Authentication
    {
        public static readonly string DatamarketAccessUri = "https://datamarket.accesscontrol.windows.net/v2/OAuth2-13";
		private string clientId;
		private string clientSecret;
		private string request;
		private AccessToken token;
		private Timer accessTokenRenewer;
		//Access token expires every 10 minutes. Renew it every 9 minutes only.
		private const int RefreshTokenDuration = 9;
		
        public Authentication(string clientId, string clientSecret)
        {
			this.clientId = clientId;
			this.clientSecret = clientSecret;
			//If clientid or client secret has special characters, encode before sending request
			this.request = string.Format("grant_type=client_credentials&client_id={0}&client_secret={1}&scope=http://api.microsofttranslator.com", HttpUtility.UrlEncode(clientId), HttpUtility.UrlEncode(clientSecret));
			this.token = HttpPost(DatamarketAccessUri, this.request);
			//renew the token every specfied minutes
            accessTokenRenewer = new Timer(new TimerCallback(OnTokenExpiredCallback), this, TimeSpan.FromMinutes(RefreshTokenDuration), TimeSpan.FromMilliseconds(-1));
        }
		public AccessToken GetAccessToken()
        {
			return this.token;
        }

		private void RenewAccessToken()
        {
            AccessToken newAccessToken = HttpPost(DatamarketAccessUri, this.request);
			//swap the new token with old one
			//Note: the swap is thread unsafe
			this.token = newAccessToken;
            Debug.WriteLine(string.Format("Renewed token for user: {0} is: {1}", this.clientId, this.token.access_token));
        }
		private void OnTokenExpiredCallback(object stateInfo)
        {
			try
            {
                RenewAccessToken();
            }
			catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Failed renewing access token. Details: {0}", ex.Message));
            }
			finally
            {
				try
                {
                    accessTokenRenewer.Change(TimeSpan.FromMinutes(RefreshTokenDuration), TimeSpan.FromMilliseconds(-1));
                }
				catch (Exception ex)
                {
                    Debug.WriteLine(string.Format("Failed to reschedule the timer to renew access token. Details: {0}", ex.Message));
                }
            }
        }
		private AccessToken HttpPost(string DatamarketAccessUri, string requestDetails)
        {
            try
            {
                //Prepare OAuth request 
                WebRequest webRequest = WebRequest.Create(DatamarketAccessUri);
                webRequest.ContentType = "application/x-www-form-urlencoded";
                webRequest.Method = "POST";
                byte[] bytes = Encoding.ASCII.GetBytes(requestDetails);
                webRequest.ContentLength = bytes.Length;
                using (Stream outputStream = webRequest.GetRequestStream())
                {
                    outputStream.Write(bytes, 0, bytes.Length);
                }
                using (WebResponse webResponse = webRequest.GetResponse())
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(AccessToken));
                    //Get deserialized object from JSON stream
                    AccessToken token = (AccessToken)serializer.ReadObject(webResponse.GetResponseStream());
                    return token;
                }
            }
            catch (WebException ex)
            {

                string msg = "";
                using (Stream rdr = ex.Response.GetResponseStream())
                {
                    StreamReader reader = new StreamReader(rdr, Encoding.Default);
                    msg = reader.ReadToEnd();
                }
                return null;
            }
        }

        

    }
}
