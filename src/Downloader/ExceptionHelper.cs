using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace Downloader
{
    internal static class ExceptionHelper
    {
        internal static bool IsMomentumError(this Exception error)
        {
            if (error.HasSource("System.Net.Http",
                "System.Net.Sockets",
                "System.Net.Security"))
                return true;

            if (error.HasTypeOf(typeof(WebException), typeof(SocketException)))
                return true;

            return false;
        }

        internal static bool HasTypeOf(this Exception exp, params Type[] types)
        {
            Exception innerException = exp;
            while (innerException != null)
            {
                foreach (Type type in types)
                {
                    if (innerException.GetType() == type)
                        return true;
                }

                innerException = innerException.InnerException;
            }

            return false;
        }

        internal static bool HasSource(this Exception exp, params string[] sources)
        {
            Exception innerException = exp;
            while (innerException != null)
            {
                foreach (string source in sources)
                {
                    if (string.Equals(innerException.Source, source, StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                innerException = innerException.InnerException;
            }

            return false;
        }

        /// <summary>
        /// Sometime a server get certificate validation error
        /// https://stackoverflow.com/questions/777607/the-remote-certificate-is-invalid-according-to-the-validation-procedure-using
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="certificate"></param>
        /// <param name="chain"></param>
        /// <param name="sslPolicyErrors"></param>
        internal static bool CertificateValidationCallBack(object sender,
            X509Certificate certificate,
            X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            // If the certificate is a valid, signed certificate, return true.
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            // If there are errors in the certificate chain, look at each error to determine the cause.
            if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateChainErrors) != 0)
            {
                if (chain?.ChainStatus is not null)
                {
                    foreach (X509ChainStatus status in chain.ChainStatus)
                    {
                        if (status.Status == X509ChainStatusFlags.NotTimeValid)
                        {
                            // If the error is for certificate expiration then it can be continued
                            return true;
                        }
                        else if ((certificate.Subject == certificate.Issuer) &&
                                 (status.Status == X509ChainStatusFlags.UntrustedRoot))
                        {
                            // Self-signed certificates with an untrusted root are valid. 
                            continue;
                        }
                        else if (status.Status != X509ChainStatusFlags.NoError)
                        {
                            // If there are any other errors in the certificate chain, the certificate is invalid,
                            // so the method returns false.
                            return false;
                        }
                    }
                }

                // When processing reaches this line, the only errors in the certificate chain are 
                // untrusted root errors for self-signed certificates. These certificates are valid
                // for default Exchange server installations, so return true.
                return true;
            }
            else
            {
                // In all other cases, return false.
                return false;
            }
        }
    }
}
