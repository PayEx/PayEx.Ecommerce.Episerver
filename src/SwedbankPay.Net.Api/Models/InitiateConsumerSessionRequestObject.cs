﻿namespace PayEx.Net.Api.Models
{
    public class InitiateConsumerSessionRequestObject
    {
        public string Operation => "initiate-consumer-session";
        
        /// <summary>
        /// The MSISDN (mobile phone number) of the payer. Format Sweden: +46707777777. Format Norway: +4799999999.
        /// </summary>
        public string Msisdn { get; set; }
        
        /// <summary>
        /// The e-mail address of the payer.
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// Consumers country of residence. Used by the consumerUi for validation on all input fields.
        /// </summary>
        public string ConsumerCountryCode { get; set; }
        
        public NationalIdentifier NationalIdentifier { get; set; }
    }
}
