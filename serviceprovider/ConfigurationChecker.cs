/*
    Copyright 2010 DanID

    This file is part of OpenOcesAPI.

    OpenOcesAPI is free software; you can redistribute it and/or modify
    it under the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation; either version 2.1 of the License, or
    (at your option) any later version.

    OpenOcesAPI is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public License
    along with OpenOcesAPI; if not, write to the Free Software
    Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA


    Note to developers:
    If you add code to this file, please take a minute to add an additional
    @author statement below.
*/
using System.DirectoryServices.Protocols;
using System.Security.Cryptography.X509Certificates;
using NLog;
using org.openoces.ooapi.certificate;
using org.openoces.ooapi.environment;
using org.openoces.ooapi.exceptions;
using org.openoces.ooapi.ldap;
using org.openoces.ooapi.pidservice;
using org.openoces.ooapi.ping;
using org.openoces.ooapi.utils;
using org.openoces.ooapi.validation;

namespace org.openoces.serviceprovider
{
    /// <summary>
    /// Use this class to check if your environment has been set up correctly.
    /// </summary>
    public class ConfigurationChecker
    {
        static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// This method is used verify that a connection can be made to the LDAP directory holding
        /// the root certificate for all environments begin set using the {@link Environments} class.
        /// </summary>
        public static void VerifyRootCertificateFromLdap()
        {
            foreach (var environment in Environments.TrustedEnvironments)
            {
                using (var connection = LdapFactory.CreateLdapConnection(environment))
                {
                    var ldapRootProp = Properties.Get("ldap.ca.dn.danid." + environment);
                    var request = new SearchRequest(ldapRootProp,(string)null, SearchScope.Base, LdapFactory.RootCertificateBinary );
                    var response = (SearchResponse)connection.SendRequest(request);
                    var bytes = (byte[])response.Entries[0].Attributes[LdapFactory.RootCertificateBinary][0];
                    var rootCertificateFromLdap = new X509Certificate2(bytes);
                    var rootCertificate = RootCertificates.LookupCertificate(environment);
                    if (rootCertificateFromLdap.Equals(rootCertificate))
                    {
                        Logger.Info("Root certificate retrieved from LDAP with DN: " + rootCertificateFromLdap.SubjectName);
                    }
                    else
                    {
                        Logger.Error("ERROR: Could not retrieve root certificate from LDAP for environment " + environment);
                    }
                }
            }
        }

        /// <summary>
        /// Checks that a full CRL can be retrieved and is valid. Expects that an environment has been set up.
        /// </summary>
        /// <returns><code>true</code> if the CRL is retrieved or else false</returns>
        public static bool VerifyFullCrl(OcesCertificate ocesCertificate)
        {
            Crl crl = CertificateRevocationHandler.RetrieveFullCrl(ocesCertificate);
            return crl != null && crl.IsValid;
        }

        /// <summary>
        /// Checks whether a connection can be made to the PID/CPR service by means of testing if 
        /// the PID service is alive and reachable.
        /// </summary>
        /// <returns><code>true</code> if the PID/CPR service can be reached by the current environment setup</returns>
        public static bool VerifyPidService()
        {
            try
            {
                PidAlivetester.PingPid();
                return true;
            }
            catch (InternalException e)
            {
                throw new ServiceProviderException("Error calling PID", e);
            }
        }

        public static bool VerifyRidService()
        {
            try
            {
                RidAliveTester.PingRid();
                return true;
            }
            catch (InternalException e)
            {
                Logger.Debug("Error calling RID: "+e);
                throw new ServiceProviderException("Error calling RID", e);
            }
        }
        /// <summary>
        /// Checks whether a connection can be made to the PID/CPR web service by means of calling 
        /// the test method on the web service
        /// </summary>
        /// <returns><code>true</code> if a connection can be made</returns>
        public static bool MakeTestConnectionToPidcprService(string pidWsUrl)
        {
            var serviceProviderClient = new PidService(pidWsUrl);
            serviceProviderClient.Test();
            return true;
        }

        /// <summary>
        /// This method calls the OCSP configured for current <code>Environment</code>.
        /// This method further validate the root certificate against the OCSP.
        /// </summary>
        /// <returns><code>true</code> if call went well, else <code>false</code></returns>
        public static bool CanCallOcsp(string ocspUrl)
        {
            return OcspAliveTester.PingOcsp(ocspUrl);
        }
    }
}
