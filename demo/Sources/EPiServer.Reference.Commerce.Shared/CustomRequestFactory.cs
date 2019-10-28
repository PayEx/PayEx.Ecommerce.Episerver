﻿using EPiServer.Commerce.Order;
using Mediachase.Commerce;
using Mediachase.Commerce.Orders.Dto;
using System.Linq;
using SwedbankPay.Checkout.Episerver;
using SwedbankPay.Checkout.Episerver.Common;
using SwedbankPay.Sdk.Models.Request;

namespace EPiServer.Reference.Commerce.Shared
{
    public class CustomRequestFactory : RequestFactory
    {
        public CustomRequestFactory(ICheckoutConfigurationLoader checkoutConfigurationLoader, IOrderGroupCalculator orderGroupCalculator, IShippingCalculator shippingCalculator) : base(checkoutConfigurationLoader, orderGroupCalculator, shippingCalculator)
        {
        }
        public override PaymentOrderRequestContainer CreatePaymentOrderRequestContainer(IOrderGroup orderGroup, IMarket market, PaymentMethodDto paymentMethodDto, string consumerProfileRef = null)
        {
            var test = base.CreatePaymentOrderRequestContainer(orderGroup, market, paymentMethodDto, consumerProfileRef);
            test.Paymentorder.Amount = 500;
            test.Paymentorder.VatAmount = 0;
            
            test.Paymentorder.OrderItems.Remove(test.Paymentorder.OrderItems.First(x => x.Reference == "SHIPPING"));
            test.Paymentorder.OrderItems.First().Amount = 500;
            test.Paymentorder.OrderItems.First().VatAmount = 0;
            
            return test;

        }
    }
}