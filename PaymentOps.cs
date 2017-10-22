using System;
using System.Linq;
using HotelAPI.GB.Models;
using System.Net.Http;
using System.Web.Http;
using System.Net;
using System.Diagnostics;
using System.Data;
using HotelAPI.GB.Operations;
using log4net;

using System.Collections.Generic;
using LittleChapel;

namespace HotelAPI.GB.Operations
{
    public static class PaymentOps
    {
        public static LittleChapel.Payment ConvertToPayment(Models.GuestPayment guestpayment, PaymentInfoModel PaymentInfo, DateTime ReservationDate, int ReservationID)
        {
            string CardAddress = (PaymentInfo.BillingZip + " " + PaymentInfo.BillingStreet1 + " " + PaymentInfo.BillingStreet2 + " " + PaymentInfo.BillingCity + " " + PaymentInfo.BillingState + " " + PaymentInfo.BillingCountry).ToString();
            LittleChapel.Payment newPayment = new LittleChapel.Payment();
            newPayment.Payee = PaymentInfo.Payee;
            newPayment.PaymentStatus = PaymentStatus.Pending;
            newPayment.Reference = string.Empty;
            newPayment.CompanyID = guestpayment.CompanyID;
            newPayment.CardZip = PaymentInfo.CVV;//Cvv
            newPayment.CardNumber = GetMaskedCardNumber(PaymentInfo.CardNumber);//CardNumber
            newPayment.Location = 9;
            newPayment.CardName = PaymentInfo.CardName;//CardName
            newPayment.CardNumberMasked = GetMaskedCardNumber(PaymentInfo.CardNumber);
            newPayment.CardMo = PaymentInfo.CardMo;
            newPayment.CardYr = PaymentInfo.CardYr;
            newPayment.CardCVV2 = string.Empty;
            newPayment.PayDate = System.DateTime.Now;
            newPayment.Description = PaymentInfo.DescriptionInfo;
            newPayment.WeddingPlanID = PaymentInfo.PlanID;
            newPayment.CardAddress = CardAddress;
            newPayment.setFee(PaymentInfo.PreviousBookDate, newPayment.CompanyID);
            newPayment.BaseAmount = PaymentInfo.Amount;
            newPayment.Fee = newPayment.GenerateFee(newPayment.BaseAmount);
            newPayment.Amount = PaymentInfo.Amount + newPayment.Fee;
            newPayment.Notes = PaymentInfo.PaymentType;
            newPayment.PayType = PaymentInfo.PayType;
            return newPayment;
        }
        public static LittleChapel.SalesOrder ConvertToSalesOrder(PaymentInfoModel PaymentInfo, decimal TotalPrice)
        {
            LittleChapel.SalesOrder salesOrder = new LittleChapel.SalesOrder();
            salesOrder.PlanID = Convert.ToInt32(PaymentInfo.PlanID);
            salesOrder.OrderDate = DateTime.Now;
            salesOrder.CreateDate = DateTime.Now;
            salesOrder.SalesOrderStatus = SalesOrderStatus.Pending;
            salesOrder.TotalPrice = TotalPrice;
            salesOrder.CompanyID = 3;
            return salesOrder;
        }
        public static LittleChapel.PaymentCard ConvertToPaymentCard(PaymentInfoModel PaymentInfo, int? CustomerID)
        {
            string lastfour;
            lastfour = PaymentInfo.CardNumber.Substring(PaymentInfo.CardNumber.Length - 4, 4);
            LittleChapel.PaymentCard paymentCard = new LittleChapel.PaymentCard();
            paymentCard.CustomerID = Convert.ToInt32(CustomerID);
            paymentCard.ExpMo = PaymentInfo.CardMo;
            paymentCard.ExpYr = PaymentInfo.CardYr;
            paymentCard.LastFour = lastfour;
            paymentCard.PayType = PaymentInfo.PayType;
            paymentCard.Zip = PaymentInfo.BillingZip;
            return paymentCard;
        }

        public static string GetMaskedCardNumber(string CardNumber)
        {
            CardNumber = CardNumber.Replace(" ", "");
            string firstTwo = CardNumber.Substring(0, 2);
            string lastFour = CardNumber.Substring(CardNumber.Length - 4, 4);
            return firstTwo + "**********" + lastFour;
        }

        public static Response ProcessPayment(Models.GuestPayment guestpayment, DateTime ReservationDate, int ContractId, int? LinkedSalesOrderID, decimal TotalPrice, ref int PaymentCardID, ref string Token, GuestAccount GuestAccount, ref bool FirstPayment, LittleChapel.WMS wms, LittleChapel.HotelsDataContext hotel, ILog Log4net)
        {
            Log4net.Info("Entered Process Payment Function");
            Response response = new Response();
            GuestPaymentResponse guestresponse = null;
            LittleChapel.Payment newPayment = null;
            string storeCardMessage = string.Empty;
            var paymentSuccess = false;
            try
            {
                PaymentInfoModel PaymentInfo = guestpayment.paymentInfo;
                bool tokenSuccess = false;
                if (!string.IsNullOrEmpty(PaymentInfo.PaymentCardID) && guestpayment.ReservationId > 0)
                {
                    guestresponse = PaymentOps.ProcessTokenPayment(guestpayment, ReservationDate, Convert.ToInt32(guestpayment.paymentInfo.PlanID), ref LinkedSalesOrderID, wms, GuestAccount, Log4net);
                    if (string.IsNullOrEmpty(guestresponse.GB_Error))
                    {
                        tokenSuccess = true;
                        paymentSuccess = true;
                        newPayment = new Payment();
                    }
                    else
                    {
                        response.Error = guestresponse.GB_Error;
                        return response;
                    }
                }
                else
                {
                    newPayment = PaymentOps.ConvertToPayment(guestpayment, PaymentInfo, ReservationDate, guestpayment.ReservationId);
                    tokenSuccess = LittleChapel.Payment.GetToken(newPayment.CardNumber, newPayment.CardMo.ToString(), newPayment.CardYr.ToString(), out storeCardMessage, newPayment.CompanyID);
                    Log4net.Info("Token Success Status:" + tokenSuccess);
                }
                newPayment.SalesOrderID = LinkedSalesOrderID;
                Log4net.Info("Finished Convert to Payment");
                HttpRequestMessage Request = new HttpRequestMessage();
                if (tokenSuccess)
                {
                    if (string.IsNullOrEmpty(PaymentInfo.PaymentCardID))
                    {
                        paymentSuccess = newPayment.Process();
                    }
                    if (paymentSuccess)
                    {
                        if (string.IsNullOrEmpty(PaymentInfo.PaymentCardID))
                        {
                            LittleChapel.PaymentCard paymentCard = wms.PaymentCards.FirstOrDefault(c => c.CustomerID == guestpayment.paymentInfo.CustomerID && c.Token == newPayment.Token);
                            if (paymentCard == null)
                            {
                                paymentCard = PaymentOps.ConvertToPaymentCard(PaymentInfo, guestpayment.paymentInfo.CustomerID);
                                paymentCard.Token = newPayment.Token;
                                wms.PaymentCards.InsertOnSubmit(paymentCard);
                                wms.SubmitChanges();
                            }
                            PaymentCardID = paymentCard.PaymentCardID;
                        }
                        Log4net.Info("Payment Success Status:" + paymentSuccess);
                        if (guestresponse != null && !string.IsNullOrEmpty(guestresponse.PaymentID))
                        {
                            if (newPayment == null)
                            {
                                newPayment = new Payment();
                            }
                            newPayment.PaymentID = Convert.ToInt32(guestresponse.PaymentID);
                            storeCardMessage = guestresponse.GB_Token;
                        }
                        if (string.IsNullOrEmpty(PaymentInfo.PaymentCardID))
                        {
                            newPayment.Payee = guestpayment.paymentInfo.Payee;
                            newPayment.PaymentStatus = PaymentStatus.Approved;
                            wms.Payments.InsertOnSubmit(newPayment);
                            wms.SubmitChanges();
                        }
                        Log4net.Info("Payment ID:" + newPayment.PaymentID);
                        Log4net.Info("Payment Entry made into Payment table" + paymentSuccess);
                        if (FirstPayment)
                        {
                            LittleChapel.Reservation reserve = hotel.Reservations.Where(x => x.ReservationId == guestpayment.ReservationId).FirstOrDefault();
                            reserve.Status = "CONFIRMED";
                            hotel.SubmitChanges();
                        }
                        int returnvalue = wms.wms_Plan_ComputeTotalPrice(Convert.ToInt32(PaymentInfo.PlanID), false);
                        if (returnvalue != 0)
                        {
                            Logging(Log4net);
                            response.Error = "failed to compute plan total price.";
                            return response;
                        }
                        Logging(Log4net);
                        response.PaymentID = newPayment.PaymentID;
                        Token = newPayment.Token;
                        response.SalesOrderID = newPayment.SalesOrderID;
                        return response;
                    }
                    else
                    {
                        #region Charge Card Fail
                        newPayment.PaymentStatus = PaymentStatus.Declined;
                        Log4net.Info("Payment Status:" + paymentSuccess);
                        string error = newPayment.Result.ErrorMessage;
                        Log4net.Info("Payment Error Message :" + error);
                        if (error.Length < 100)
                        {
                            newPayment.Reference = error;
                        }
                        else
                        {
                            newPayment.Reference = error.Substring(0, 99);
                        }
                        Log4net.Info("ErrorMessage:" + error);
                        wms.Payments.InsertOnSubmit(newPayment);
                        wms.SubmitChanges();
                        #endregion
                        Logging(Log4net);
                        response.Error = "There was a problem processing the card: " + error;
                        return response;
                    }
                }
            }
            catch (Exception ex)
            {
                Log4net.Info("ErrorMessage:" + ex.Message);
                Logging(Log4net);
                response.Error = "An error has occurred.";
                return response;
            }
            Logging(Log4net);
            response.Error = "An error has occurred.";
            return response;
        }
        public static void Logging(ILog Log4net)
        {
            Log4net.Info("Leaving Process Payment Function");
        }
        /// <summary>
        ///
        /// </summary>
        /// <param name="LinkedSalesOrderID"></param>
        /// <param name="guestpayment"></param>
        /// <param name="TotalPrice"></param>
        /// <param name="wms"></param>
        /// <param name="Log4net"></param>
        /// <param name="newPayment"></param>
        /// <returns>LittleChapel.Payment</returns>
        public static void SalesOrder(int? LinkedSalesOrderID, Models.GuestPayment guestpayment, GuestAccount guestAccount, decimal TotalPrice, WMS wms, ILog Log4net, ref LittleChapel.Payment newPayment)
        {
            if (LinkedSalesOrderID == null || LinkedSalesOrderID == 0)
            {
                LittleChapel.SalesOrder salesOrder = PaymentOps.ConvertToSalesOrder(guestpayment.paymentInfo, TotalPrice);
                salesOrder.CustomerID = guestpayment.paymentInfo.CustomerID;
                wms.SalesOrders.InsertOnSubmit(salesOrder);
                wms.SubmitChanges();
                Log4net.Info("SalesOrder ID:" + salesOrder.SalesOrderID);
                newPayment.SalesOrderID = salesOrder.SalesOrderID;
            }
            else
            {
                LittleChapel.SalesOrder salesOrder = wms.SalesOrders.Where(x => x.SalesOrderID == LinkedSalesOrderID).FirstOrDefault();
                if (salesOrder != null)
                {
                    //salesOrder.TotalPaidNew = guestpayment.paymentInfo.Amount;
                    Log4net.Info("SalesOrder ID:" + LinkedSalesOrderID);
                    newPayment.SalesOrderID = LinkedSalesOrderID;
                }
            }
        }
        public static GuestPaymentResponse ProcessTokenPayment(GuestPayment guestpayment, DateTime ReservationDate, int? PlanID, ref int? LinkedSalesOrderID, LittleChapel.WMS wms, GuestAccount guestAccount, ILog Log4net)
        {
            GuestPaymentResponse guestPaymentResponse = new GuestPaymentResponse();
            try
            {
                HttpRequestMessage Request = new HttpRequestMessage();
                PaymentInfoModel PaymentInfo = guestpayment.paymentInfo;
                string CardAddress = (PaymentInfo.BillingZip + " " + PaymentInfo.BillingStreet1 + " " + PaymentInfo.BillingStreet2 + " " + PaymentInfo.BillingCity + " " + PaymentInfo.BillingState + " " + PaymentInfo.BillingCountry).ToString();
                var plan = wms.Plans.Where(x => x.PlanID == PlanID).FirstOrDefault();
                LittleChapel.PaymentCard PaymentCard = wms.PaymentCards.FirstOrDefault(c => c.PaymentCardID == Convert.ToInt32(PaymentInfo.PaymentCardID));
                LittleChapel.Payment newPayment = new LittleChapel.Payment();
                newPayment.Payee = PaymentInfo.Payee;
                newPayment.PaymentStatus = PaymentStatus.Pending;
                newPayment.Reference = string.Empty;
                newPayment.Location = 9;
                newPayment.CompanyID = guestpayment.CompanyID;
                newPayment.CardZip = !String.IsNullOrEmpty(PaymentCard.Zip) ? PaymentCard.Zip : (plan.Customer.BillingAddress == null || String.IsNullOrEmpty(plan.Customer.BillingAddress.Zip) ? "" : plan.Customer.BillingAddress.Zip);
                newPayment.CardNumber = string.Empty;
                newPayment.CardName = string.Empty;
                newPayment.CardNumberMasked = "************" + PaymentCard.LastFour;
                newPayment.CardMo = PaymentCard.ExpMo;
                newPayment.CardYr = PaymentCard.ExpYr;
                newPayment.CardCVV2 = string.Empty;
                newPayment.PayDate = System.DateTime.Now;
                newPayment.Token = PaymentCard.Token;
                guestPaymentResponse.GB_Token = PaymentCard.Token;
                newPayment.Description = string.IsNullOrEmpty(PaymentInfo.DescriptionInfo) ? string.Empty : PaymentInfo.DescriptionInfo;
                newPayment.WeddingPlanID = PlanID;
                newPayment.CardAddress = CardAddress;
                newPayment.setFee(PaymentInfo.PreviousBookDate, newPayment.CompanyID);
                newPayment.BaseAmount = PaymentInfo.Amount;
                newPayment.Fee = newPayment.GenerateFee(newPayment.BaseAmount);
                newPayment.Amount = newPayment.BaseAmount + newPayment.Fee;
                newPayment.PayType = (int)PaymentType.CreditCard;
                newPayment.Notes = PaymentInfo.PaymentType;
                bool paymentSuccessful = newPayment.Process();
                newPayment.Notes = newPayment.Notes;
                //newPayment.Notes = newPayment.Notes.Replace("(MES)", "");
                wms.Payments.InsertOnSubmit(newPayment);
                wms.SubmitChanges();
                if (paymentSuccessful)
                {
                    guestPaymentResponse.ResponseCode = 200;
                    guestPaymentResponse.GB_GuestID = Convert.ToString(guestpayment.GuestID);
                    //guestPaymentResponse.CA_PackageID = plan.PackageID.ToString();
                    guestPaymentResponse.GB_Token = newPayment.Token;
                    guestPaymentResponse.GB_Reference = newPayment.Reference;
                    guestPaymentResponse.PaymentID = Convert.ToString(newPayment.PaymentID);
                    return guestPaymentResponse;
                }
                {
                    guestPaymentResponse.ResponseCode = 500;
                    guestPaymentResponse.GB_GuestID = null;
                    guestPaymentResponse.GB_Error = newPayment.Result.ErrorMessage;
                    //guestPaymentResponse.CA_PlanID = null;
                    //guestPaymentResponse.CA_PackageID = null;
                    guestPaymentResponse.GB_Token = "";
                    //guestPaymentResponse.GB_Reference = null;
                    return guestPaymentResponse;
                }
            }
            catch (Exception ex)
            {
                //Log4net.Info("ErrorMessage:" + ex.Message);
                guestPaymentResponse.GB_Error = "A problem has occurred ";
                return guestPaymentResponse;

            }
        }
    }
    public class Response
    {
        public int PaymentID { get; set; }
        public int? SalesOrderID { get; set; }
        public string Error { get; set; }
    }
}
