﻿namespace PayEx.Net.Api.Models
{
    using System.Collections.Generic;

    public class PaymentOrderRequest
    {
        /// <summary>
        /// The operation that the payment order is supposed to perform.
        /// </summary>
        public string Operation { get; set; }

        /// <summary>
        /// The currency of the payment.
        /// </summary>
        public string Currency { get; set; }

        /// <summary>
        /// The amount including VAT in the lowest monetary unit of the currency. E.g. 10000 equals 100.00 NOK and 5000 equals 50.00 NOK.
        /// </summary>
        public int Amount { get; set; }

        /// <summary>
        /// The amount of VAT in the lowest monetary unit of the currency. E.g. 10000 equals 100.00 NOK and 5000 equals 50.00 NOK.
        /// </summary>
        public int VatAmount { get; set; }

        /// <summary>
        /// The description of the payment order.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The user agent of the payer.
        /// </summary>
        public string UserAgent { get; set; }
        
        /// <summary>
        /// The language of the payer.
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// Determines if a payment token should be generated. A payment token is primarily used to enable future One-Click Payments or recurring payments - with the same token - through server-to-server calls. Default value is false
        /// </summary>
        public bool GeneratePaymentToken { get; set; }

        public Urls Urls { get; set; }

        public PayeeInfo PayeeInfo { get; set; }

        public Payer Payer { get; set; }

        /// <summary>
        /// The keys and values that should be associated with the payment order. Can be additional identifiers and data you want to associate with the payment.
        /// </summary>
        public Dictionary<string, object> MetaData { get; set; }

        //The List of items that will affect how the payment is performed.
        public List<Item> Items { get; set; }
    }
}
