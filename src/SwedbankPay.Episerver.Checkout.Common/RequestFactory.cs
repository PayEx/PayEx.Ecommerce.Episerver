﻿using EPiServer.Business.Commerce.Exception;
using EPiServer.Commerce.Order;
using EPiServer.Globalization;
using EPiServer.ServiceLocation;
using EPiServer.Web;

using Mediachase.Commerce;
using Mediachase.Commerce.Markets;
using Mediachase.Commerce.Orders;
using Mediachase.Commerce.Orders.Dto;

using SwedbankPay.Episerver.Checkout.Common.Helpers;
using SwedbankPay.Sdk;
using SwedbankPay.Sdk.Consumers;
using SwedbankPay.Sdk.PaymentOrders;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;

namespace SwedbankPay.Episerver.Checkout.Common
{
    [ServiceConfiguration(typeof(IRequestFactory))]
    public class RequestFactory : IRequestFactory
    {
        private readonly ICheckoutConfigurationLoader _checkoutConfigurationLoader;
        private readonly IOrderGroupCalculator _orderGroupCalculator;
        private readonly IShippingCalculator _shippingCalculator;
        private readonly SwedbankPayTaxCalculator _swedbankPayTaxCalculator;
        private readonly IReturnLineItemCalculator _returnLineItemCalculator;

        public RequestFactory(
            ICheckoutConfigurationLoader checkoutConfigurationLoader,
            IOrderGroupCalculator orderGroupCalculator,
            IShippingCalculator shippingCalculator,
            SwedbankPayTaxCalculator swedbankPayTaxCalculator, IReturnLineItemCalculator returnLineItemCalculator)
        {
            _checkoutConfigurationLoader = checkoutConfigurationLoader ??
                                           throw new ArgumentNullException(nameof(checkoutConfigurationLoader));
            _orderGroupCalculator = orderGroupCalculator;
            _shippingCalculator = shippingCalculator;
            _swedbankPayTaxCalculator = swedbankPayTaxCalculator;
            _returnLineItemCalculator = returnLineItemCalculator;
        }

        public virtual PaymentOrderRequest GetPaymentOrderRequest(
            IOrderGroup orderGroup, IMarket market, PaymentMethodDto paymentMethodDto, string description, string consumerProfileRef = null)
        {
            if (orderGroup == null)
                throw new ArgumentNullException(nameof(orderGroup));
            if (market == null)
                throw new ArgumentNullException(nameof(market));

            var marketCountry = CountryCodeHelper.GetTwoLetterCountryCode(market.Countries.FirstOrDefault());
            if (string.IsNullOrWhiteSpace(marketCountry))
                throw new ConfigurationException($"Please select a country in Commerce Manager for market {orderGroup.MarketId}");

            var firstShipment = orderGroup.GetFirstShipment();
            var orderItems = GetOrderItems(market, orderGroup.Currency, firstShipment.ShippingAddress, firstShipment.LineItems).ToList();
            orderItems.Add(GetShippingOrderItem(firstShipment, market));

            return CreatePaymentOrderRequest(orderGroup, market, consumerProfileRef, orderItems, description);
        }

        public virtual ConsumersRequest GetConsumerResourceRequest(Language language,
            IEnumerable<RegionInfo> shippingAddressRestrictedToCountryCodes, EmailAddress email = null,
            Msisdn msisdn = null, NationalIdentifier nationalIdentifier = null)
        {
            return new ConsumersRequest(language, shippingAddressRestrictedToCountryCodes, Operation.Initiate);
        }

        public virtual ReversalRequest GetReversalRequest(IEnumerable<ILineItem> lineItems, IMarket market, IShipment shipment, bool addShipmentInOrderItem = true, string description = "Reversing payment.")
        {
            var orderItems = GetOrderItems(market, shipment.ParentOrderGroup.Currency, shipment.ShippingAddress, lineItems).ToList();
            if (addShipmentInOrderItem)
            {
                orderItems.Add(GetShippingOrderItem(shipment, market));
            }

            return new ReversalRequest(Amount.FromDecimal(GetTotalAmountIncludingVatAsDecimal(orderItems)), Amount.FromDecimal(GetTotalVatAmountAsDecimal(orderItems)), orderItems, description, DateTime.Now.Ticks.ToString());
        }

        public virtual CaptureRequest GetCaptureRequest(IPayment payment, IMarket market, IShipment shipment, bool addShipmentInOrderItem = true, string description = "Capturing payment.")
        {
            var orderItems = GetOrderItems(market, shipment.ParentOrderGroup.Currency, shipment.ShippingAddress, shipment.LineItems).ToList();
            if (addShipmentInOrderItem)
            {
                orderItems.Add(GetShippingOrderItem(shipment, market));
            }

            return new CaptureRequest(Amount.FromDecimal(GetTotalAmountIncludingVatAsDecimal(orderItems)), Amount.FromDecimal(GetTotalVatAmountAsDecimal(orderItems)), orderItems, description, DateTime.Now.Ticks.ToString());
        }

        public virtual CancelRequest GetCancelRequest(string description = "Cancelling purchase order.")
        {
            return new CancelRequest(DateTime.Now.Ticks.ToString(), description);
        }

        public virtual AbortRequest GetAbortRequest()
        {
            return new AbortRequest();
        }

        public virtual UpdateRequest GetUpdateRequest(IOrderGroup orderGroup)
        {
            var totals = _orderGroupCalculator.GetOrderGroupTotals(orderGroup);
            return new UpdateRequest(Amount.FromDecimal(totals.Total.Amount), Amount.FromDecimal(totals.TaxTotal));
        }

        private IEnumerable<OrderItem> GetOrderItems(IMarket market, Currency currency, IOrderAddress shippingAddress, IEnumerable<ILineItem> lineItems)
        {
            return lineItems.Select(item =>
            {
                var unitPrice = item.PlacedPrice;
                var extendedPrice = item.ReturnQuantity > 0 ? _returnLineItemCalculator.GetExtendedPrice(item as IReturnLineItem, currency) : item.GetExtendedPrice(currency);

                var salesTax = _swedbankPayTaxCalculator.GetSalesTax(item, market, shippingAddress, extendedPrice);
                var vatPercent = (int)_swedbankPayTaxCalculator.GetTaxPercentage(item, market, shippingAddress, TaxType.SalesTax) * 100;
                var amount = market.PricesIncludeTax ? extendedPrice : extendedPrice + salesTax;

                return new OrderItem(item.LineItemId.ToString(), item.DisplayName, OrderItemType.Product, "FASHION",
                    item.ReturnQuantity > 0 ? item.ReturnQuantity : item.Quantity , "PCS", Amount.FromDecimal(unitPrice), vatPercent, Amount.FromDecimal(amount),
                    Amount.FromDecimal(salesTax));
            });
        }

        private decimal GetTotalAmountIncludingVatAsDecimal(IEnumerable<OrderItem> orderItems)
        {
            return (decimal)orderItems.Sum(x => x.Amount.Value) / 100;
        }
        private decimal GetTotalVatAmountAsDecimal(IEnumerable<OrderItem> orderItems)
        {
            return (decimal)orderItems.Sum(x => x.VatAmount.Value) / 100;
        }

        private PaymentOrderRequest CreatePaymentOrderRequest(IOrderGroup orderGroup, IMarket market, string consumerProfileRef, List<OrderItem> orderItems, string description)
        {
            var configuration = _checkoutConfigurationLoader.GetConfiguration(market.MarketId);
            var currencyCode = orderGroup.Currency.CurrencyCode;

            var payer = !string.IsNullOrEmpty(consumerProfileRef)
                ? new Payer
                {
                    ConsumerProfileRef = consumerProfileRef
                }
                : null;
            
            return new PaymentOrderRequest(Operation.Purchase, new CurrencyCode(currencyCode), Amount.FromDecimal(GetTotalAmountIncludingVatAsDecimal(orderItems)), Amount.FromDecimal(GetTotalVatAmountAsDecimal(orderItems)), description,
                HttpContext.Current.Request.UserAgent, CultureInfo.CreateSpecificCulture(ContentLanguage.PreferredCulture.Name),
                false, GetMerchantUrls(orderGroup, market), new PayeeInfo(new Guid(configuration.MerchantId),
                    DateTime.Now.Ticks.ToString()), orderItems: orderItems, payer: payer);
        }

        private OrderItem GetShippingOrderItem(IShipment shipment, IMarket market)
        {
            var currency = shipment.ParentOrderGroup.Currency;
            var shippingVatAmount = _shippingCalculator.GetShippingTax(shipment, market, currency).Round();

            var shippingAmount = _shippingCalculator.GetShippingCost(shipment, market, currency);
            
            var amount = market.PricesIncludeTax ? shippingAmount :  shippingAmount + shippingVatAmount;
            
            int vatPercent;

            if (market.PricesIncludeTax)
            {
                vatPercent = (int)((shippingVatAmount / (shippingAmount - shippingVatAmount)).Round() * 10000);
            }
            else vatPercent = (int)((shippingVatAmount / (shippingAmount)).Round() * 10000);
            
            return new OrderItem("SHIPPING", "SHIPPINGFEE", OrderItemType.ShippingFee, "NOTAPPLICABLE", 1, "PCS",
                Amount.FromDecimal(shippingAmount.Amount), vatPercent, Amount.FromDecimal(amount.Amount),
                Amount.FromDecimal(shippingVatAmount.Amount));
        }

        private Urls GetMerchantUrls(IOrderGroup orderGroup, IMarket market)
        {
            var checkoutConfiguration = _checkoutConfigurationLoader.GetConfiguration(market.MarketId);

            Uri ToFullSiteUrl(Func<CheckoutConfiguration, Uri> fieldSelector)
            {
                if (fieldSelector(checkoutConfiguration) == null)
                    return null;

                var uriBuilder = new UriBuilder(fieldSelector(checkoutConfiguration));
                var query = HttpUtility.ParseQueryString(uriBuilder.Query);
                query["orderGroupId"] = orderGroup.OrderLink.OrderGroupId.ToString();

                uriBuilder.Query = query.ToString();
                if (!uriBuilder.Uri.IsAbsoluteUri)
                    return new Uri(SiteDefinition.Current.SiteUrl, uriBuilder.Uri.PathAndQuery);

                return uriBuilder.Uri;
            }

            var cancelUrl = checkoutConfiguration.PaymentUrl != null ? ToFullSiteUrl(c => c.CancelUrl) : null;

            return new Urls(checkoutConfiguration.HostUrls, ToFullSiteUrl(c => c.CompleteUrl),
                checkoutConfiguration.TermsOfServiceUrl, ToFullSiteUrl(c => c.CancelUrl), ToFullSiteUrl(c => c.PaymentUrl),
                ToFullSiteUrl(c => c.CallbackUrl), checkoutConfiguration.LogoUrl);
        }
    }
}