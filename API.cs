using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using HotelAPI.GB.Models;
using LittleChapel;
using System.Diagnostics;
using System.Data;
using HotelAPI.GB.Operations;
using log4net;
using System.Reflection;
using System.Collections.Generic;
using System.Transactions;
using HotelAPI.DW.Controllers;
using MoreLinq;
using HotelAPI.Utils;


namespace HotelAPI.GB.Controllers
{
    /// <summary>
    /// GuestBooking Controller
    /// </summary>
    [RoutePrefix("api/GB")]
    public class GBController : BaseController
    {
        static ILog Log4net = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        /// <summary>
        /// This method is for Payment Checkout
        /// </summary>
        /// <param name="reserve"></param>
        /// <returns>Reserve</returns>
        [HttpPut]
        [Route("reservation")]
        public HttpResponseMessage Reservation(Reserve reserve)
        {
            if (string.IsNullOrEmpty(reserve.ActionType))
            {
                reserve.ActionType = "Create";
            }
            Log4net.Info("Entered Reservation Function.");
            HttpResponseMessage ValidationResponse = Check(reserve.reservation.CreatedBy, reserve.reservation.CompanyId, false);
            Log4net.Info("Validation Response:" + ValidationResponse);
            if (ValidationResponse.IsSuccessStatusCode)
            {
                using (var scope = new TransactionScope())
                {
                    try
                    {
                        reserve.reservation.CreatedDate = DateTime.Now;
                        reserve.reservation.IsValid = true;
                        int ReservationId = CreateReservation(reserve.reservation);
                        Log4net.Info("Reservation ID:" + ReservationId);
                        if (ReservationId > 0 && reserve != null && reserve.reservation != null && reserve.reservation.Rooms > 0)
                        {
                            int roomBookedId = 0;
                            int guestdetailResponseId = 0;
                            for (int RoomsBooked = 0; RoomsBooked < reserve.reservation.Rooms; RoomsBooked++)
                            {
                                reserve.roombooked[RoomsBooked].ReservationId = ReservationId;
                                reserve.roombooked[RoomsBooked].CreatedBy = reserve.reservation.CreatedBy;
                                reserve.roombooked[RoomsBooked].CreatedDate = reserve.reservation.CreatedDate;
                                reserve.roombooked[RoomsBooked].CompanyId = reserve.reservation.CompanyId;
                                reserve.roombooked[RoomsBooked].IsValid = true;
                                roomBookedId = RoomBooked(reserve.roombooked[RoomsBooked]);
                                Log4net.Info("RoomBooked ID:" + roomBookedId);
                                if (roomBookedId > 0)
                                {
                                    for (int intI = 0; intI < reserve.roombooked[RoomsBooked].GuestDetails.Count(); intI++)
                                    {
                                        reserve.roombooked[RoomsBooked].GuestDetails[intI].ReservationId = ReservationId;
                                        reserve.roombooked[RoomsBooked].GuestDetails[intI].RoomBookedId = roomBookedId;
                                        reserve.roombooked[RoomsBooked].GuestDetails[intI].ContractId = reserve.reservation.ContractId;
                                        reserve.roombooked[RoomsBooked].GuestDetails[intI].CreatedBy = reserve.reservation.CreatedBy;
                                        reserve.roombooked[RoomsBooked].GuestDetails[intI].CreatedDate = reserve.reservation.CreatedDate;
                                        reserve.roombooked[RoomsBooked].GuestDetails[intI].CompanyId = reserve.reservation.CompanyId;
                                        reserve.roombooked[RoomsBooked].GuestDetails[intI].GuestAccId = Convert.ToInt32(reserve.reservation.CreatedBy);
                                        reserve.roombooked[RoomsBooked].GuestDetails[intI].IsValid = true;
                                        guestdetailResponseId = GuestDetail(reserve.roombooked[RoomsBooked].GuestDetails[intI]);
                                        Log4net.Info("GuestDetail ID:" + guestdetailResponseId);
                                        if (guestdetailResponseId == 0)
                                        {
                                            Logging(Log4net);
                                            return CatchMessage(null);
                                        }
                                    }
                                }
                                else
                                {
                                    Logging(Log4net);
                                    return CatchMessage(null);
                                }
                            }
                            if (ReservationId > 0 && roomBookedId > 0 && guestdetailResponseId > 0)
                            {
                                Models.RoomInventory roomInventory = new Models.RoomInventory();
                                roomInventory.CompanyId = reserve.reservation.CompanyId;
                                roomInventory.RoomCatId = reserve.roombooked[0].RoomCatId;
                                roomInventory.TotalHeld = reserve.reservation.Rooms;
                                roomInventory.ContractId = reserve.reservation.ContractId;
                                roomInventory.CreatedBy = reserve.reservation.CreatedBy;
                                roomInventory.CreatedDate = reserve.reservation.CreatedDate;
                                roomInventory.TotalPartiallySold = 0;
                                roomInventory.TotalSold = 0;
                                roomInventory.TotalHold = true;
                                int RoomInventoryId = RoomInvCommon(ReservationId, roomInventory);
                                if (RoomInventoryId > 0)
                                {
                                    scope.Complete();
                                    ReservationResponse reserveResponse = new ReservationResponse();
                                    reserveResponse.ReservationID = ReservationId;
                                    reserveResponse.RoomInventoryID = RoomInventoryId;
                                    return Request.CreateResponse(HttpStatusCode.OK, reserveResponse);
                                }
                                else
                                {
                                    Logging(Log4net);
                                    return CatchMessage(null);
                                }
                            }
                            else
                            {
                                Logging(Log4net);
                                return Request.CreateResponse(HttpStatusCode.InternalServerError, "One or all status codes failed.");
                            }
                        }
                        else
                        {
                            Logging(Log4net);
                            return CatchMessage(null);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logging(Log4net);
                        return CatchMessage(ex);
                    }
                }
            }
            else
            {
                Logging(Log4net);
                return ValidationResponse;
            }
        }

        /// <summary>
        /// This method Logs the Catch Exceptions also sends Emails
        /// </summary>
        /// <param name="ex"></param>
        /// <returns>Http Status Code</returns>
        public HttpResponseMessage CatchMessage(Exception ex)
        {
            if (ex != null && ex.Message != null)
            {
                StackTrace st = new StackTrace(ex, true);
                StackFrame frame = st.GetFrame(0);
                Log4net.Info("Error Message:" + ex.Message + " LineNumber:" + frame.GetFileLineNumber());
                SendErrorEmail(ex, "GuestBooking Controller");
            }
            return Request.CreateResponse(HttpStatusCode.InternalServerError, " ErrorMessage : An unknown error has occurred.");
        }



        /// <summary>
        /// This is only for logging
        /// </summary>
        /// <param name="Log4net"></param>
        public void Logging(ILog Log4net)
        {
            Log4net.Info("Leaving Reservation Function.");
        }




        /// <summary>
        /// This is for Inserting into Reservation Table
        /// </summary>
        /// <param name="reservations"></param>
        /// <returns>Reservation ID</returns>
        //[HttpPut]
        //[Route("reserve")]
        public int CreateReservations(Models.Reservation reservations)
        {
            try
            {
                LittleChapel.Reservation reservation = new LittleChapel.Reservation();
                reservation.Adults = reservations.Adults;
                reservation.Children = reservations.Children;
                reservation.Rooms = reservations.Rooms;
                reservation.TotalPrice = reservations.TotalPrice;
                reservation.CompanyId = reservations.CompanyId;
                reservation.ContractId = reservations.ContractId;
                reservation.Status = Convert.ToString(reservations.Status);
                reservation.CreatedBy = reservations.CreatedBy;
                reservation.InactiveDate = DateTime.Now;
                reservation.CreatedDate = reservations.CreatedDate;
                reservation.InactiveDate = DateTime.Now;
                reservation.Deleted = false;
                hdc.Reservations.InsertOnSubmit(reservation);
                hdc.SubmitChanges();
                return reservation.ReservationId;
            }
            catch (Exception ex)
            {
                CatchMessage(ex);
                return 0;
            }
        }

        /// <summary>
        /// This method is used for getting Roombooked 
        /// </summary>
        /// <param name="ReservationId"></param>
        /// <returns>Models.Roombooked</returns>
        [HttpGet]
        [Route("roombooked/info/{ReservationId}")]
        public Models.Roombooked GetRoomBooked(int ReservationId)
        {

            Models.Roombooked roomsbooked = new Models.Roombooked();
            LittleChapel.ReservedRoom roombooked = hdc.ReservedRooms.Where(x => x.ReservationId == ReservationId).FirstOrDefault();
            roomsbooked.ReservationId = roombooked.ReservationId;
            roomsbooked.RoomCatId = Convert.ToInt16(roombooked.RoomCategoryId);
            roomsbooked.StartDate = roombooked.StartDate;
            roomsbooked.EndDate = roombooked.EndDate;
            roomsbooked.CompanyId = roombooked.CompanyId;
            roomsbooked.CreatedBy = roombooked.CreatedBy;
            roomsbooked.CreatedDate = Convert.ToDateTime(roombooked.CreatedDate.ToString("MM/dd/yyyy"));
            roomsbooked.ModifiedBy = roombooked.ModifiedBy;
            roomsbooked.ModifiedDate = DateTime.Now;
            return roomsbooked;
        }


        /// <summary>
        /// This is Get for fetching Guest Data
        /// </summary>
        /// <param name="ReservationId"></param>
        /// <returns>Models.GuestDetail</returns>
        [HttpGet]
        [Route("GuestDetails/info/{ReservationId}")]
        public Models.GuestDetail GetGuestDetails(int ReservationId)
        {
            List<LittleChapel.GuestDetail> listguestdetails = new List<LittleChapel.GuestDetail>();
            Models.GuestDetail guestdetail = new Models.GuestDetail();
            List<Models.GuestDetail> listguestdetail = new List<Models.GuestDetail>();
            listguestdetails = hdc.GuestDetails.Where(x => x.ReservationId == ReservationId).ToList();
            for (int intI = 0; intI < listguestdetails.Count(); intI++)
            {
                LittleChapel.GuestDetail guestdetails = new LittleChapel.GuestDetail();
                guestdetails = listguestdetails[intI];
                guestdetail.FirstName = guestdetails.FirstName;
                guestdetail.LastName = guestdetails.LastName;
                guestdetail.IsChild = Convert.ToBoolean(guestdetails.IsChild);
                guestdetail.CompanyId = guestdetails.CompanyId;
                guestdetail.ReservationId = Convert.ToInt32(guestdetails.ReservationId);
                guestdetail.RoomBookedId = Convert.ToInt32(guestdetails.RoomBookedId);
                guestdetail.SugarCRMId = guestdetails.SugarCRMId;
                guestdetail.Price = guestdetails.Price;
                guestdetail.InsurancePrice = guestdetails.InsurancePrice;
                guestdetail.TravelInsurance = Convert.ToBoolean(guestdetails.TravelInsurance);
                guestdetail.CreatedBy = guestdetails.CreatedBy;
                guestdetail.CreatedDate = Convert.ToDateTime(guestdetails.CreatedDate.ToString("MM/dd/yyyy"));
                guestdetail.ModifiedBy = guestdetails.ModifiedBy;
                DateTime dateTime;
                bool blnDateTimeorNot = DateTime.TryParse(Convert.ToString(guestdetails.ModifiedDate), out dateTime);
                if (blnDateTimeorNot)
                {
                    guestdetail.ModifiedDate = dateTime;
                }
                else
                {
                    guestdetail.ModifiedDate = null;
                }
                guestdetail.Deleted = false;
                guestdetail.ContractId = Convert.ToInt32(guestdetails.ContractId);
                listguestdetail.Add(guestdetail);
            }
            return guestdetail;
        }


        /// <summary>
        /// This method is for Updating and Creating entry in Reservations Table
        /// </summary>
        /// <param name="reservations"></param>
        /// <returns>ReservationId</returns>
        [AcceptVerbs("POST", "PUT")]
        [Route("roombooked")]
        public int CreateReservation(Models.Reservation reservations)
        {
            try
            {
                HttpResponseMessage ValidationResponse = Check(reservations.CreatedBy, reservations.CompanyId, reservations.IsValid);
                Log4net.Info("Reservation Validation Response:" + ValidationResponse);
                if (ValidationResponse.IsSuccessStatusCode)
                {
                    return CreateReservations(reservations);
                }
                else
                {
                    Log4net.Info("Reservation Validation Response:" + ValidationResponse);
                }
                return 0;
            }
            catch (Exception ex)
            {
                CatchMessage(ex);
                return 0;
            }
        }

        [HttpGet]
        [Route("BookingList/{ContractID}/{GuestAccID}")]
        public HttpResponseMessage GuestDetailsByIds(int ContractID, int GuestAccID)
        {
            try
            {
                PastBook pastbooking = new PastBook();
                LittleChapel.GuestAccount guestaccreservation = null;
                if (ContractID > 0 && GuestAccID > 0)
                {
                    guestaccreservation = hdc.GuestAccounts.Where(x => x.GuestAccId == GuestAccID && x.ContractId == ContractID && x.Deleted == false).FirstOrDefault();
                }
                else if (ContractID > 0)
                {
                    guestaccreservation = hdc.GuestAccounts.Where(x => x.ContractId == ContractID && x.Deleted == false).FirstOrDefault();
                }
                if (guestaccreservation != null)
                {
                    List<LittleChapel.Reservation> reservation = null;
                    if (GuestAccID > 0)
                    {
                        reservation = hdc.Reservations.Where(x => x.CreatedBy == Convert.ToString(guestaccreservation.GuestAccId) && (x.Status == "CONFIRMED" || x.Status == "CANCELLED" || x.Status == "FAILED") && x.Deleted == false).ToList();
                    }
                    else
                    {
                        reservation = hdc.Reservations.Where(x => x.ContractId == ContractID && (x.Status == "CONFIRMED" || x.Status == "CANCELLED" || x.Status == "FAILED") && x.Deleted == false).ToList();
                    }
                    pastbooking.Reservation = new List<Reservations>();
                    for (int reserve = 0; reserve < reservation.Count(); reserve++)
                    {
                        Models.Reservations re = new Models.Reservations();
                        re.ReservationID = reservation[reserve].ReservationId;
                        re.TotalPrice = reservation[reserve].TotalPrice;
                        var salesorder = wms.SalesOrders.Where(x => x.SalesOrderID == reservation[reserve].LinkedSalesOrderId).Select(x => x.CreateDate).FirstOrDefault();
                        re.CreatedDate = Convert.ToDateTime(salesorder).ToString("MM/dd/yyyy");
                        var contract = hdc.Contracts.Where(x => x.ContractId == reservation[reserve].ContractId).FirstOrDefault();
                        var resort = hdc.Resorts.Where(x => x.ResortId == contract.ResortId).FirstOrDefault();
                        re.ResortDesignation = resort.Resort_Designation__c;
                        var paymentdetail = wms.Payments.Where(x => x.SalesOrderID == reservation[reserve].LinkedSalesOrderId).
                            Select(x => new Payed
                            {
                                PaymentID = x.PaymentID,
                                PaymentDate = x.PayDate,
                                Amount = x.Amount,
                                CardType = Enum.GetName(typeof(PaymentType), x.PayType),
                                PayType = x.Notes.Replace("(MES)", String.Empty),
                                Status = Enum.GetName(typeof(PaymentStatus), x.PaymentStatus),
                                TrailingCardNumbers = x.CardNumberMasked.Substring(x.CardNumberMasked.Length - 4, 4),
                                Fee = x.Fee,
                                BaseAmount = x.BaseAmount
                            }).ToList();
                        re.Pay = paymentdetail;
                        var roomcat = hdc.ReservedRooms.Where(x => x.ReservationId == reservation[reserve].ReservationId).GroupBy(x => x.RoomCategoryId).Select(x => x.Key).ToList();
                        if (roomcat.Count() > 0)
                        {
                            re.RoomCategory = new List<Models.RoomCategory>();
                            for (int roomcategory = 0; roomcategory < roomcat.Count(); roomcategory++)
                            {
                                Models.RoomCategory roomct = new Models.RoomCategory();
                                roomct.RoomCategoryId = roomcat[roomcategory].Value;
                                var roomcatname = hdc.RoomCategories.Where(x => x.RoomCategoryId == roomcat[roomcategory].Value).Select(x => new { x.RoomCategoryId, x.Name, x.RoomDesignation }).FirstOrDefault();
                                roomct.Name = roomcatname.Name;
                                roomct.RoomDesignation = roomcatname.RoomDesignation;
                                roomct.RoomCategoryId = roomcatname.RoomCategoryId;
                                var rooms = hdc.ReservedRooms.Where(x => x.ReservationId == reservation[reserve].ReservationId && x.RoomCategoryId == roomct.RoomCategoryId).GroupBy(x => x.RoomBookedId).Select(x => x.Key).ToList();
                                roomct.Rooms = new List<Room>();
                                for (int RoomsBooked = 0; RoomsBooked < rooms.Count(); RoomsBooked++)
                                {
                                    var rb = hdc.ReservedRooms.Where(x => x.RoomBookedId == rooms[RoomsBooked]).FirstOrDefault();
                                    Room rom = new Room();
                                    rom.RoomBookedId = rb.RoomBookedId;
                                    rom.RoomDescription = rb.Notes;
                                    rom.StartDate = rb.StartDate;
                                    rom.EndDate = rb.EndDate;
                                    rom.NoofNights = Convert.ToDecimal((rb.EndDate - rb.StartDate).TotalDays);
                                    rom.RoomDesignation = roomcatname.RoomDesignation;
                                    var gd = hdc.GuestDetails.Where(x => x.RoomBookedId == rom.RoomBookedId && x.ReservationId == re.ReservationID).ToList();
                                    rom.GuestDetails = new List<Models.GuestID>();
                                    for (int GuestDetails = 0; GuestDetails < gd.Count(); GuestDetails++)
                                    {
                                        Models.GuestID guestdetails = new Models.GuestID();
                                        guestdetails.GuestId = gd[GuestDetails].GuestId;
                                        guestdetails.FirstName = gd[GuestDetails].FirstName;
                                        guestdetails.LastName = gd[GuestDetails].LastName;
                                        guestdetails.Age = gd[GuestDetails].Age;
                                        guestdetails.IsChild = gd[GuestDetails].IsChild;
                                        guestdetails.GuestPrice = gd[GuestDetails].Price;
                                        guestdetails.InsurancePrice = gd[GuestDetails].InsurancePrice;
                                        rom.GuestDetails.Add(guestdetails);
                                    }
                                    roomct.Rooms.Add(rom);
                                }
                                re.RoomCategory.Add(roomct);
                            }
                        }
                        else
                        {
                            pastbooking.ErrorMessage = "No RoomCategory Found.";
                        }
                        pastbooking.Reservation.Add(re);
                    }
                }
                return Request.CreateResponse(HttpStatusCode.OK, pastbooking);
            }
            catch (Exception ex)
            {
                CatchMessage(ex);
                return Request.CreateResponse(HttpStatusCode.InternalServerError, "An unknown error has occurred.");
            }
        }


        /// <summary>
        /// This method is for sending Emails
        /// </summary>
        /// <param name="e"></param>
        /// <param name="ControllerName"></param>
        public static void SendErrorEmail(Exception e, string ControllerName)
        {
            Log4net.Info("\n\tSending Error Mail.");
            Email email = new Email();
            email.To = "exceptions@everafter.com";
            email.Subject = "Exception in " + ControllerName;
            email.Body = "\nError Message" + e.Message + "\nStackTrace" + e.StackTrace;
            if (!email.Send())
            {
                Log4net.Info("\n\tError Email failed.");
            }
            else
            {
                Log4net.Info("\n\ttError Email Sent.");
            }
        }




        #region
        /// <summary>
        /// This is for inserting data into RoomBooked
        /// </summary>
        /// <param name="roombooked"></param>
        /// <returns>RoomBookedId</returns>
        public int RoomBooking(Models.Roombooked roombooked)
        {
            try
            {
                LittleChapel.ReservedRoom roomsbooked = new LittleChapel.ReservedRoom();
                roomsbooked.ReservationId = roombooked.ReservationId;
                roomsbooked.RoomCategoryId = roombooked.RoomCatId;
                roomsbooked.StartDate = roombooked.StartDate;
                roomsbooked.EndDate = roombooked.EndDate;
                roomsbooked.CompanyId = roombooked.CompanyId;
                roomsbooked.CreatedBy = roombooked.CreatedBy;
                roomsbooked.CreatedDate = DateTime.Now;
                roomsbooked.Deleted = false;
                roomsbooked.Notes = roombooked.Notes;
                hdc.ReservedRooms.InsertOnSubmit(roomsbooked);
                hdc.SubmitChanges();
                return roomsbooked.RoomBookedId;
            }
            catch (Exception ex)
            {
                CatchMessage(ex);
                return 0;
            }
        }

        [HttpPost]
        [Route("GuestDetail/AdditionalTime")]
        public HttpResponseMessage ReservationTimeStamp(TimeStamp timestamp)
        {
            try
            {
                var StampChange = hdc.Reservations.Where(x => x.ReservationId == timestamp.ReservationId && (x.Status == "CONFIRMED" || x.Status == "ON HOLD") && x.Deleted == false).FirstOrDefault();
                if (StampChange != null)
                {
                    StampChange.InactiveDate = Convert.ToDateTime(StampChange.InactiveDate).AddMinutes(10);
                    StampChange.ModifiedBy = timestamp.CreatedBy;
                    StampChange.ModifiedDate = DateTime.Now;
                    hdc.SubmitChanges();
                }
                else
                {
                    return Request.CreateResponse(HttpStatusCode.RequestTimeout);
                }
                return Request.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                CatchMessage(ex);
                return Request.CreateResponse(HttpStatusCode.InternalServerError, "An unknown error has occurred.");
            }
        }


        /// <summary>
        /// This api is being used to Update and Insert RoomBooked
        /// </summary>
        /// <param name="roombooked"></param>
        /// <returns>RoomBookingId</returns>
        [AcceptVerbs("POST", "PUT")]
        [Route("roombooked")]
        public int RoomBooked(Models.Roombooked roombooked)
        {
            try
            {
                HttpResponseMessage ValidationResponse = Check(roombooked.CreatedBy, roombooked.CompanyId, roombooked.IsValid);
                Log4net.Info("Reservation Validation Response:" + ValidationResponse);
                if (ValidationResponse.IsSuccessStatusCode)
                {
                    return RoomBooking(roombooked);
                }
                else
                {
                    Log4net.Info("RoomBooked Validation Response:" + ValidationResponse);
                }
                return 0;
            }
            catch (Exception ex)
            {
                CatchMessage(ex);
                return 0;
            }
        }
        #endregion




        /// <summary>
        /// This method checks if createdby and company id is there or not
        /// </summary>
        /// <param name="CreatedBy"></param>
        /// <param name="CompanyID"></param>
        /// <param name="Valid"></param>
        /// <returns>HttpStatusCode</returns>
        public HttpResponseMessage Check(string CreatedBy, int CompanyID, bool Valid)
        {
            if (Valid)
            {
                return Request.CreateResponse(HttpStatusCode.OK);
            }
            Log4net.Info("Entered Check Function of CreatedBy and CompanyID");
            if (string.IsNullOrEmpty(CreatedBy) || CompanyID != 3)
            {
                Log4net.Info("CreatedBy :" + CreatedBy);
                Log4net.Info("CompanyID :" + CompanyID);
                return Request.CreateResponse(HttpStatusCode.BadRequest, "missing created by or company id.");
            }
            Log4net.Info("Leaving Check Function of CreatedBy and CompanyID");
            return Request.CreateResponse(HttpStatusCode.OK);
        }



        /// <summary>
        /// This method Inserts Data into GuestDetails Table
        /// </summary>
        /// <param name="guestdetails"></param>
        /// <returns>GuestId</returns>
        public int GuestDetailing(Models.GuestDetail guestdetails)
        {
            try
            {
                LittleChapel.GuestDetail guestdetail = new LittleChapel.GuestDetail();
                guestdetail.FirstName = guestdetails.FirstName;
                guestdetail.MiddleName = guestdetails.MiddleName;
                guestdetail.LastName = guestdetails.LastName;
                guestdetail.IsChild = guestdetails.Age > 0 ? true : guestdetails.IsChild;
                guestdetail.CompanyId = guestdetails.CompanyId;
                guestdetail.ReservationId = guestdetails.ReservationId;
                guestdetail.RoomBookedId = guestdetails.RoomBookedId;
                guestdetail.SugarCRMId = guestdetails.SugarCRMId;
                guestdetail.Email = guestdetails.EmailID;
                guestdetail.Price = guestdetails.Price;
                guestdetail.InsurancePrice = guestdetails.InsurancePrice;
                guestdetail.Age = guestdetails.Age;
                guestdetail.Email = guestdetails.Email;
                guestdetail.PhoneNo = guestdetails.PhoneNo;
                guestdetail.TravelInsurance = guestdetails.InsurancePrice > 0 ? true : guestdetail.TravelInsurance;
                guestdetail.CreatedBy = guestdetails.CreatedBy;
                guestdetail.GuestAccId = guestdetails.GuestAccId;
                guestdetail.CreatedDate = DateTime.Now;
                guestdetail.ContractId = guestdetails.ContractId;
                guestdetail.Deleted = false;
                hdc.GuestDetails.InsertOnSubmit(guestdetail);
                hdc.SubmitChanges();
                return guestdetail.GuestId;
            }
            catch (Exception ex)
            {
                CatchMessage(ex);
                return 0;
            }
        }


        /// <summary>
        /// This method inserts data into guest details
        /// </summary>
        /// <param name="guestdetails"></param>
        /// <returns>GuestId</returns>
        [AcceptVerbs("POST", "PUT")]
        [Route("guestdetail")]
        public int GuestDetail(Models.GuestDetail guestdetails)
        {

            HttpResponseMessage ValidationResponse = Check(guestdetails.CreatedBy, guestdetails.CompanyId, guestdetails.IsValid);
            Log4net.Info("Reservation Validation Response:" + ValidationResponse);
            if (ValidationResponse.IsSuccessStatusCode)
            {
                try
                {
                    return GuestDetailing(guestdetails);
                }
                catch (Exception ex)
                {
                    CatchMessage(ex);
                    return 0;
                }
            }
            else
            {
                Log4net.Info("Guest Details Validation Response:" + ValidationResponse);
            }
            return 0;

        }





        /// <summary>
        /// This method is Search of GuestBooking
        /// </summary>
        /// <param name="searchRequest"></param>
        /// <returns>SearchRequest</returns>
        [HttpPut]
        [Route("guestbooking/search")]
        public HttpResponseMessage Search(SearchRequest searchRequest)
        {
            SearchAPIResponse SearchAPIResponse = new SearchAPIResponse();
            try
            {

                List<SearchList> FilteredRooms = new List<Models.SearchList>();
                SearchList searchModelList = new SearchList();
                List<SearchModel> smList = new List<SearchModel>();
                SearchModel searchModel;

                int NoOfRooms = searchRequest.roomRequestCollection.Count;
                decimal NoofNights = Convert.ToDecimal((searchRequest.CheckoutDate - searchRequest.CheckinDate).TotalDays);
                List<WholeSalerQuote> wsq = hdc.WholeSalerQuotes.Where(C => C.ContractId == searchRequest.ContractID &&
                C.MinimunNights <= NoofNights && C.MaximumAdults >= searchRequest.roomRequestCollection[0].adults &&
                C.MinimumOccupancy <= searchRequest.roomRequestCollection[0].adults
                &&
                C.MaximumOccupancy >= searchRequest.roomRequestCollection[0].adults + searchRequest.roomRequestCollection[0].children.Count
                && (C.Deleted == false || C.Deleted == null)).ToList();


                SearchAPIResponse.SearchList = new List<SearchList>();
                searchModelList.searchModel = new List<SearchModel>();
                foreach (WholeSalerQuote durationwsq in wsq)
                {
                    // var Filtered = durationwsq.DurationPrices.Where(c => c.Nights >= NoofNights && (c.Deleted == false || c.Deleted == null)).ToList();
                    var Filtered = durationwsq.DurationPrices.Where(c => c.Deleted == false || c.Deleted == null).ToList();
                    if (Filtered.Count > 0)
                    {
                        LittleChapel.RoomInventory roomInventory = hdc.RoomInventories.Where(x => x.ContractId == searchRequest.ContractID && x.RoomCategoryId == durationwsq.RoomCategory.RoomCategoryId && (x.Deleted == false || x.Deleted == null)).FirstOrDefault();
                        foreach (var filter in Filtered)
                        {
                            searchModel = new SearchModel();
                            searchModel.ChildPrices = new List<ChildPrices>();
                            searchModel.RoomCategoryId = durationwsq.RoomCategory.RoomCategoryId;
                            searchModel.RoomCategoryName = durationwsq.RoomCategory.Name;
                            searchModel.RoomDescription = durationwsq.RoomCategory.RoomDescription;
                            searchModel.RoomDesignation = durationwsq.RoomCategory.RoomDesignation;
                            searchModel.MinimumOccupants = durationwsq.MinimumOccupancy;
                            searchModel.MaximumOccupants = durationwsq.MaximumOccupancy;
                            searchModel.MaximumAdults = durationwsq.MaximumAdults;
                            searchModel.MaximumChildren = durationwsq.MaximumChildren;

                            searchModel.RemainingRooms = roomInventory.TotalRooms - (roomInventory.TotalSold + roomInventory.TotalHeld);
                            var rates = filter.WSQRoomRates;
                            decimal totalAdultPrice = 0;
                            decimal totalChildPrice = 0;
                            decimal ChildPrice = 0;
                            decimal addNights = 0;

                            var adultRates = rates.Where(c => c.NumberOfAdults == searchRequest.roomRequestCollection[0].adults && (c.Deleted == false || c.Deleted == null)).FirstOrDefault();


                            if (adultRates != null)
                            {
                                searchModel.AdultBasePrice = adultRates.BasePrice;

                                addNights = Convert.ToDecimal(NoofNights - durationwsq.MinimunNights < 0 ? 0 : NoofNights - durationwsq.MinimunNights);

                                if (addNights > 0)
                                {
                                    searchModel.AdultBasePrice += adultRates.AdditionalNightPrice * addNights;
                                }
                                totalAdultPrice = Convert.ToDecimal((adultRates.BasePrice + (addNights * adultRates.AdditionalNightPrice)) * searchRequest.roomRequestCollection[0].adults);

                                foreach (int childage in searchRequest.roomRequestCollection[0].children)
                                {
                                    ChildPrices childprice = new ChildPrices();
                                    var childRates = rates.Where(c => c.ChildFromAge <= childage && c.ChildToAge >= childage && (c.Deleted == false || c.Deleted == null)).FirstOrDefault();
                                    if (childRates != null)
                                    {
                                        if (addNights > 0)
                                        {
                                            childprice.Age = childage;
                                            childprice.ChildBasePrice = childRates.BasePrice + childRates.AdditionalNightPrice * addNights;
                                            searchModel.ChildPrices.Add(childprice);
                                        }
                                        else
                                        {
                                            childprice.Age = childage;
                                            childprice.ChildBasePrice = childRates.BasePrice;
                                            searchModel.ChildPrices.Add(childprice);
                                        }

                                        ChildPrice = Convert.ToDecimal(childRates.BasePrice + (addNights * childRates.AdditionalNightPrice));
                                        totalChildPrice += ChildPrice;
                                    }
                                }
                                searchModel.TotalAdultPrice = totalAdultPrice;
                                searchModel.TotalChildPrice = totalChildPrice;
                                searchModel.AdditionalNights = addNights;
                                searchModelList.searchModel.Add(searchModel);
                            }
                        }
                        SearchAPIResponse.SearchList.Add(searchModelList);
                        searchModelList = new SearchList();
                        searchModelList.searchModel = new List<SearchModel>();
                    }
                }
                var FilteringRemainingRooms = SearchAPIResponse.SearchList.Where(c => c.searchModel.Any(x => x.RemainingRooms >= NoOfRooms && x.MinimumOccupants <= searchRequest.roomRequestCollection.First().adults + searchRequest.roomRequestCollection.First().children.Count && x.MaximumOccupants >= searchRequest.roomRequestCollection.First().adults + searchRequest.roomRequestCollection.First().children.Count && x.MaximumAdults >= searchRequest.roomRequestCollection.First().adults && x.MaximumChildren >= Convert.ToByte(searchRequest.roomRequestCollection.First().children.Count)));
                return Request.CreateResponse(HttpStatusCode.OK, FilteringRemainingRooms);
            }
            catch (Exception ex)
            {
                return CatchMessage(ex);
            }
        }


        /// <summary>
        /// This API method is for updating RoomInventory table
        /// </summary>
        /// <param name="ReservationID"></param>
        /// <param name="roomInventories"></param>
        /// <returns>RoomInventory</returns>
        public int RoomInvCommon(int ReservationID, Models.RoomInventory roomInventories)
        {
            LittleChapel.RoomInventory roomInventory = null;
            try
            {
                var reservations = hdc.Reservations.Where(x => x.ReservationId == ReservationID && x.Deleted == false).FirstOrDefault();
                var roomcat = hdc.ReservedRooms.Where(x => x.ReservationId == reservations.ReservationId && x.Deleted == false).GroupBy(x => x.RoomCategoryId).Select(x => x.Key).ToList();
                for (int roomCat = 0; roomCat < roomcat.Count(); roomCat++)
                {
                    var reservedrooms = hdc.ReservedRooms.Where(x => x.ReservationId == ReservationID && x.RoomCategoryId == roomcat[roomCat].Value && x.Deleted == false).Count();
                    roomInventory = hdc.RoomInventories.Where(x => x.ContractId == reservations.ContractId && x.RoomCategoryId == roomcat[roomCat].Value && x.CompanyId == 3 && x.Deleted == false).FirstOrDefault();
                    if (roomInventory != null)
                    {
                        if (roomInventories.TotalHold)
                        {
                            roomInventory.TotalHeld = Convert.ToByte(roomInventory.TotalHeld + reservedrooms);
                        }
                        else if (roomInventories.TotalSeld)
                        {
                            roomInventory.TotalSold = Convert.ToByte(roomInventory.TotalSold + reservedrooms);
                            roomInventory.TotalHeld = Convert.ToByte(roomInventory.TotalHeld - reservedrooms);
                        }
                        roomInventory.Deleted = false;
                        roomInventory.ModifiedDate = DateTime.Now;
                        roomInventory.ModifiedBy = roomInventories.CreatedBy;
                        hdc.SubmitChanges();
                    }
                }
                return roomInventory == null ? 0 : roomInventory.RoomInventoryId;
            }
            catch (Exception ex)
            {
                CatchMessage(ex);
                return 0;
            }
        }

        //[HttpPut]
        //[Route("MakePayment")]
        //public HttpResponseMessage MakePay(Models.GuestPayment guestpayment)
        //{
        //    Response response = null;
        //    GuestPaymentResponse responses = null;
        //    try
        //    {
        //        var guestAccount = hdc.GuestAccounts.Where(x => x.ContractId == guestpayment.paymentInfo.ContractID && x.GuestAccId == guestpayment.GuestID && x.Deleted == false);
        //        if (guestAccount != null)
        //        {
        //            decimal PaymentAmount = 0;
        //            decimal BalanceDue = 0;
        //            int PaymentCardID = 0;
        //            string Token = string.Empty;
        //            decimal ActualAmount = guestpayment.paymentInfo.Amount;
        //            var reservation = hdc.Reservations.Where(x => x.ContractId == guestpayment.paymentInfo.ContractID && x.CreatedBy == Convert.ToString(guestpayment.GuestID) && x.Status == "CONFIRMED" && x.Deleted == false).ToList();
        //            if (reservation != null)
        //            {
        //                if (reservation.Count() > 1 || guestpayment.ReservationId == 0 && (string.IsNullOrEmpty(guestpayment.paymentInfo.PaymentCardID) || Convert.ToInt32(guestpayment.paymentInfo.PaymentCardID) == 0))
        //                {
        //                    var reserves = reservation.ToList();
        //                    for (int intI = 0; intI < reserves.Count(); intI++)
        //                    {
        //                        if (reserves[intI].LinkedSalesOrderId != null && reserves[intI].LinkedSalesOrderId > 0)
        //                        {
        //                            var salesorder = wms.SalesOrders.Where(x => x.SalesOrderID == reserves[intI].LinkedSalesOrderId && x.SalesOrderStatus != SalesOrderStatus.Cancelled).FirstOrDefault();
        //                            if (salesorder != null)
        //                            {
        //                                var ReserveRow = from P in hdc.Reservations.Distinct()
        //                                                 join pi in hdc.Contracts on P.ContractId equals pi.ContractId
        //                                                 join evnt in hdc.EventDetails on pi.EventID equals evnt.EventID
        //                                                 where P.ReservationId == reserves[intI].ReservationId && P.Deleted == false
        //                                                 select new
        //                                                 {
        //                                                     PlanID = evnt.Planid,
        //                                                     ReservationDate = P.CreatedDate,
        //                                                     EventID = evnt.EventID,
        //                                                     TotalPrice = P.TotalPrice,
        //                                                     ContractID = P.ContractId,
        //                                                     LinkedSalesOrderID = P.LinkedSalesOrderId
        //                                                 };
        //                                if (salesorder.TotalPaid < salesorder.TotalPrice)
        //                                {
        //                                    if (ActualAmount > 0)
        //                                    {
        //                                        guestpayment.ReservationId = reserves[intI].ReservationId;
        //                                        guestpayment.LinkedSalesOrderID = Convert.ToString(salesorder.SalesOrderID);
        //                                        PaymentAmount = salesorder.TotalPrice - salesorder.TotalPaid;
        //                                        if (PaymentAmount <= ActualAmount)
        //                                        {
        //                                            guestpayment.paymentInfo.Amount = PaymentAmount;
        //                                            ActualAmount = ActualAmount - PaymentAmount;
        //                                        }
        //                                        else if (ActualAmount <= PaymentAmount)
        //                                        {
        //                                            BalanceDue = PaymentAmount - ActualAmount;
        //                                            guestpayment.paymentInfo.Amount = ActualAmount;
        //                                            if (BalanceDue > 0)
        //                                            {
        //                                                ActualAmount = 0;
        //                                            }
        //                                        }
        //                                        response = PaymentOps.ProcessPayment(guestpayment, (ReserveRow.Single()).ReservationDate, (ReserveRow.Single()).ContractID, (ReserveRow.Single()).LinkedSalesOrderID, (ReserveRow.Single()).TotalPrice,ref PaymentCardID,ref Token, wms, hdc, Log4net);
        //                                    }
        //                                }
        //                                if (intI == (reserves.Count() - 1) && ActualAmount > 0)
        //                                {
        //                                    guestpayment.paymentInfo.Amount = ActualAmount;

        //                                    response = PaymentOps.ProcessPayment(guestpayment, (ReserveRow.Single()).ReservationDate, (ReserveRow.Single()).ContractID, (ReserveRow.Single()).LinkedSalesOrderID, (ReserveRow.Single()).TotalPrice,ref PaymentCardID, ref Token, wms, hdc, Log4net);
        //                                }
        //                            }
        //                        }
        //                    }
        //                }
        //                else
        //                {
        //                    var reservationFirst = hdc.Reservations.Where(x => x.ContractId == guestpayment.paymentInfo.ContractID && x.CreatedBy == Convert.ToString(guestpayment.GuestID) && x.Deleted == false).FirstOrDefault();
        //                    if (reservation != null)
        //                    {
        //                        var contract = hdc.Contracts.Where(x => x.ContractId == reservationFirst.ContractId).FirstOrDefault(); // reserve.ContractId
        //                        var Event = hdc.EventDetails.Where(x => x.EventID == contract.EventID).FirstOrDefault(); // reserve.ContractId
        //                        responses = PaymentOps.ProcessTokenPayment(guestpayment, reservationFirst.CreatedDate, Event.Planid, reservationFirst.LinkedSalesOrderId, wms);
        //                        response.Error = responses.GB_Error;
        //                        response.PaymentID = Convert.ToInt32(responses.PaymentID);
        //                    }
        //                }
        //            }
        //        }
        //        //response. = responses.GB_GuestID;
        //        //response.Error = responses.GB_Error;
        //        //response.Error = responses.GB_Error;
        //        return Request.CreateResponse(HttpStatusCode.OK, response);
        //    }
        //    catch (Exception ex)
        //    {
        //        return CatchMessage(ex);
        //    }
        //}



        /// <summary>
        /// This api is used for payment
        /// </summary>
        /// <param name="guestpayment"></param>
        /// <returns>Models.GuestPayment</returns>
        [HttpPut]
        [Route("pay")]
        public HttpResponseMessage GuestPay(Models.GuestPayment guestpayment)
        {
            HttpResponseMessage ValidationResponse = Check(guestpayment.CreatedBy, guestpayment.CompanyID, guestpayment.Valid);
            if (ValidationResponse.IsSuccessStatusCode)
            {
                int ReservationID = guestpayment.ReservationId;
                WMS wms = new WMS();
                Response response = null;
                bool FirstPayment = false;
                int PaymentCardID = Convert.ToInt32(guestpayment.paymentInfo.PaymentCardID);
                string Token = string.Empty;
                try
                {
                    if (Convert.ToInt32(guestpayment.LinkedSalesOrderID) > 0)
                    {
                        var salesOrder = wms.SalesOrders.Where(x => x.SalesOrderID == Convert.ToInt32(guestpayment.LinkedSalesOrderID) && x.SalesOrderStatus != SalesOrderStatus.Cancelled).FirstOrDefault();
                        if (salesOrder != null)
                        {
                            var Reservation = hdc.Reservations.Where(x => x.LinkedSalesOrderId == salesOrder.SalesOrderID && x.Status == "CONFIRMED" && x.Deleted == false).FirstOrDefault();
                            guestpayment.ReservationId = Reservation.ReservationId;
                        }
                    }
                    if (guestpayment.ReservationId != 0)
                    {
                        var reservation = hdc.Reservations.Where(x => (x.Status == "ON HOLD" || x.Status == "CONFIRMED") && x.ReservationId == guestpayment.ReservationId && x.Deleted == false).FirstOrDefault();
                        if (reservation != null)
                        {
                            var ReserveRow = from P in hdc.Reservations.Distinct()
                                             join pi in hdc.Contracts on P.ContractId equals pi.ContractId
                                             join evnt in hdc.EventDetails on pi.EventID equals evnt.EventID
                                             where P.ReservationId == guestpayment.ReservationId && P.Deleted == false
                                             select new
                                             {
                                                 PlanID = evnt.Planid,
                                                 ReservationDate = P.CreatedDate,
                                                 EventID = evnt.EventID,
                                                 CreatedBy = P.CreatedBy,
                                                 TotalPrice = P.TotalPrice,
                                                 ContractID = P.ContractId,
                                                 LinkedSalesOrderID = P.LinkedSalesOrderId

                                             };
                            if ((ReserveRow.Single()).LinkedSalesOrderID == null)
                            {
                                FirstPayment = true;
                            }
                            var GuestAccount = hdc.GuestAccounts.Where(x => x.GuestAccId == Convert.ToInt32((ReserveRow.Single()).CreatedBy) && x.ContractId == (ReserveRow.Single()).ContractID && x.Deleted == false).FirstOrDefault();
                            if (GuestAccount == null)
                            {
                                response.Error = "GuestAccount is invalid";
                                return Request.CreateResponse(HttpStatusCode.BadRequest, response);
                            }
                            var Customer = from cu in wms.Customers
                                           where cu.Email == GuestAccount.Email
                                           select new
                                           {
                                               CustomerID = cu.CustomerID,
                                               GuestName = cu.GroomNameFirst + " " + cu.GroomNameLast,
                                               AddressID = cu.BillingAddressID
                                           };
                            if (Customer == null || Customer.Count() == 0)
                            {
                                Log4net.Info("Entered Customer Scope");
                                Customer customer = new Customer();
                                customer.BrideNameFirst = string.Empty;
                                customer.BrideNameLast = string.Empty;
                                customer.GroomNameFirst = GuestAccount.FirstName;
                                customer.GroomNameLast = GuestAccount.LastName;
                                customer.CompanyID = GuestAccount.CompanyId;
                                customer.CustomerType = 2;

                                //Add Address
                                Address newAddress = new Address();
                                newAddress.Street = string.IsNullOrEmpty(guestpayment.paymentInfo.BillingStreet1) ? "" : guestpayment.paymentInfo.BillingStreet1;
                                newAddress.Street2 = string.IsNullOrEmpty(guestpayment.paymentInfo.BillingStreet2) ? "" : guestpayment.paymentInfo.BillingStreet2;
                                newAddress.Unit = string.Empty;
                                newAddress.City = string.IsNullOrEmpty(guestpayment.paymentInfo.BillingCity) ? "" : guestpayment.paymentInfo.BillingCity;
                                newAddress.State = string.IsNullOrEmpty(guestpayment.paymentInfo.BillingState) ? "" : guestpayment.paymentInfo.BillingState;
                                newAddress.Zip = string.IsNullOrEmpty(guestpayment.paymentInfo.BillingZip) ? "" : guestpayment.paymentInfo.BillingZip;
                                newAddress.Country = string.IsNullOrEmpty(guestpayment.paymentInfo.BillingCountry) ? "" : guestpayment.paymentInfo.BillingCountry;
                                wms.Addresses.InsertOnSubmit(newAddress);
                                wms.SubmitChanges();
                                customer.BillingAddressID = newAddress.AddressID;
                                customer.ShippingAddressID = newAddress.AddressID;
                                customer.DayPhone = (string.IsNullOrEmpty(GuestAccount.PhoneNo) ? string.Empty : GuestAccount.PhoneNo);
                                customer.EvePhone = (string.IsNullOrEmpty(GuestAccount.PhoneNo) ? string.Empty : GuestAccount.PhoneNo);
                                customer.Fax = string.Empty;
                                customer.IsEmailOptedOut = false;
                                customer.ReferralID = 0;
                                customer.AffiliateID = 0;
                                customer.AuthUserID = 0;
                                customer.IsRenewal = false;
                                if (GuestAccount.Email != "")
                                {
                                    customer.Email = GuestAccount.Email;
                                }
                                wms.Customers.InsertOnSubmit(customer);
                                wms.SubmitChanges();
                                guestpayment.paymentInfo.Payee = GuestAccount.FirstName + " " + GuestAccount.LastName;
                                guestpayment.paymentInfo.CustomerID = customer.CustomerID;
                            }
                            else
                            {
                                guestpayment.paymentInfo.Payee = (Customer.FirstOrDefault()).GuestName;
                                guestpayment.paymentInfo.CustomerID = (Customer.FirstOrDefault()).CustomerID;
                                //var Address = wms.Addresses.Where(x => x.AddressID == (Customer.FirstOrDefault()).AddressID).FirstOrDefault();
                                //Address.Country = string.IsNullOrEmpty(guestpayment.paymentInfo.BillingCountry) ? "" : guestpayment.paymentInfo.BillingCountry;
                            }
                            SalesOrder(FirstPayment, (ReserveRow.Single()).LinkedSalesOrderID, ref guestpayment, GuestAccount, (ReserveRow.Single()).TotalPrice, wms, Log4net);
                            if (FirstPayment)
                            {
                                reservation.LinkedSalesOrderId = Convert.ToInt32(guestpayment.LinkedSalesOrderID);
                                wms.SubmitChanges();
                            }
                            var previousReservations = hdc.Reservations.Where(x => x.Status == "CONFIRMED" && x.CreatedBy == Convert.ToString(GuestAccount.GuestAccId) && x.Deleted == false).OrderBy(x => x.CreatedDate).FirstOrDefault();
                            guestpayment.paymentInfo.PreviousBookDate = previousReservations == null ? DateTime.Now : previousReservations.CreatedDate;
                            int PlanID = 0;
                            if (ReserveRow != null && Customer != null && (ReserveRow.Single()).PlanID != 0)
                            {
                                if (int.TryParse(Convert.ToString((ReserveRow.Single()).PlanID), out PlanID))
                                {
                                    Log4net.Info("Guest Pay PlanID:" + (ReserveRow.Single()).PlanID);
                                    guestpayment.paymentInfo.PlanID = PlanID;
                                    Log4net.Info("Processing payment.");
                                    response = PaymentOps.ProcessPayment(guestpayment, (ReserveRow.Single()).ReservationDate, (ReserveRow.Single()).ContractID, (ReserveRow.Single()).LinkedSalesOrderID, (ReserveRow.Single()).TotalPrice, ref PaymentCardID, ref Token, GuestAccount, ref FirstPayment, wms, hdc, Log4net);
                                    if (response.PaymentID != 0 && response.SalesOrderID != 0 && response.SalesOrderID != null)
                                    {
                                        Log4net.Info("Room Inventory Entry.");
                                        var EventDateTime = hdc.EventDetails.Where(X => X.EventID == (ReserveRow.Single()).EventID).FirstOrDefault();
                                        DateTime dateTime = Convert.ToDateTime(EventDateTime.PaymentDueDate);
                                        if (FirstPayment)
                                        {
                                            Models.RoomInventory roomInventories = new Models.RoomInventory();
                                            roomInventories.ModifiedBy = guestpayment.CreatedBy;
                                            roomInventories.TotalSeld = true;
                                            RoomInvCommon(guestpayment.ReservationId, roomInventories);
                                            HttpResponseMessage paymentSchedule = PaymentSchedule(Convert.ToInt32(response.SalesOrderID), dateTime, Token, PaymentCardID);
                                            if (!paymentSchedule.IsSuccessStatusCode)
                                            {
                                                response.Error = "Problem occurred trying to schedule Payment";
                                                //return paymentSchedule;
                                            }
                                        }
                                        try
                                        {
                                            EmailUtilities eu = new EmailUtilities();
                                            eu.CreatePaymentReciept(Convert.ToInt32(response.SalesOrderID), guestpayment.GuestID);
                                        }
                                        catch (Exception)
                                        {

                                        }
                                        return Request.CreateResponse(HttpStatusCode.OK, response);
                                    }
                                    else if (response.Error.Contains("There was a problem processing the card: A transaction was recently approved using this card for this exact same amount. Please wait 1 hour and try again.") || response.Error.Contains("A transaction was recently approved using this card for this exact same amount. Please wait 1 hour and try again."))
                                    {
                                        return Request.CreateResponse(HttpStatusCode.BadRequest, response);
                                    }
                                    else
                                    {
                                        return Request.CreateResponse(HttpStatusCode.InternalServerError, response);
                                    }
                                }
                                else
                                {
                                    response.Error = "Invalid Plan or Package ID.";
                                    Log4net.Info(response.Error);
                                    return Request.CreateResponse(HttpStatusCode.InternalServerError, "Invalid Plan or Package ID.");
                                }
                            }
                            else
                            {
                                return Request.CreateResponse(HttpStatusCode.BadRequest, "No Customer or No data found in DB.");
                            }
                        }
                        else
                        {
                            return Request.CreateResponse(HttpStatusCode.BadRequest, "Reservation Id is null.");
                        }
                    }
                    else
                    {
                        return Request.CreateResponse(HttpStatusCode.BadRequest, "Reservation Status is not on hold.");
                    }
                }
                catch (Exception ex)
                {
                    return CatchMessage(ex);
                }
            }
            else
            {
                return ValidationResponse;
            }
        }
        public static void SalesOrder(bool FirstPayment, int? LinkedSalesOrderID, ref Models.GuestPayment guestpayment, GuestAccount guestAccount, decimal TotalPrice, WMS wms, ILog Log4net)
        {
            if (FirstPayment)
            {
                LittleChapel.SalesOrder salesOrder = PaymentOps.ConvertToSalesOrder(guestpayment.paymentInfo, TotalPrice);
                salesOrder.CustomerID = guestpayment.paymentInfo.CustomerID;
                wms.SalesOrders.InsertOnSubmit(salesOrder);
                wms.SubmitChanges();
                Log4net.Info("SalesOrder ID:" + salesOrder.SalesOrderID);
                guestpayment.LinkedSalesOrderID = Convert.ToString(salesOrder.SalesOrderID);
            }
            else
            {
                LittleChapel.SalesOrder salesOrder = wms.SalesOrders.Where(x => x.SalesOrderID == LinkedSalesOrderID).FirstOrDefault();
                if (salesOrder != null)
                {
                    //salesOrder.TotalPaidNew = guestpayment.paymentInfo.Amount;
                    Log4net.Info("SalesOrder ID:" + LinkedSalesOrderID);
                    guestpayment.LinkedSalesOrderID = Convert.ToString(LinkedSalesOrderID);
                }
            }
        }



        /// <summary>
        /// Create schedule payments for the Link Sales order 
        /// </summary>
        /// <param name="SalesOrderID"> Link Sales Order ID</param>
        /// <param name="FinalPaymentDate"> Final Payment Due Date</param>
        /// <param name="Token">payment token</param>
        /// <returns>The Payment Status</returns>
        public HttpResponseMessage PaymentSchedule(int SalesOrderID, DateTime FinalPaymentDate, string Token, int PaymentCardID)
        {
            try
            {
                DateTime startdate = DateTime.Now;
                //PaymentCard storedCard;

                //get salesorder
                SalesOrder salesorder = wms.SalesOrders.Where(c => c.SalesOrderID == SalesOrderID).SingleOrDefault();
                if (wms.Payments.Where(c => c.SalesOrderID == salesorder.SalesOrderID).Any())
                {
                    #region EMI Calculation
                    if (salesorder.TotalPaid < salesorder.TotalPrice)
                    {
                        int monthcount = (FinalPaymentDate.Year - startdate.Year) * 12 + FinalPaymentDate.Month - startdate.Month;
                        //storedCard = GetPaymentCard(Token, salesorder);
                        ////if (storedCard == null)
                        ////{
                        ////    return Request.CreateResponse(System.Net.HttpStatusCode.BadRequest, "Error: " + "Could not find the card details");
                        ////}
                        #region Same month
                        if (monthcount == 0)
                        {
                            createPaymentSchedule(PaymentCardID, salesorder, FinalPaymentDate);
                        }
                        #endregion
                        #region More than 1 emi
                        else
                        {
                            // get EMI Dates
                            var dates = GetDatesBetween(startdate, FinalPaymentDate, monthcount);
                            decimal BalanceAmount = Math.Round(salesorder.TotalPrice - salesorder.TotalPaid / dates.Count, 2);
                            #region insert EMI Dates
                            foreach (var dt in dates)
                            {
                                createPaymentSchedule(PaymentCardID, salesorder, dt);
                            }
                        }

                        Logging(Log4net);
                        return Request.CreateResponse(System.Net.HttpStatusCode.OK);
                        #endregion
                        #endregion

                    }
                    else
                    {
                        Logging(Log4net);
                        return Request.CreateResponse(HttpStatusCode.NoContent);
                    }
                }
                else
                {
                    Logging(Log4net);
                    return Request.CreateResponse(HttpStatusCode.NoContent);
                }
                #endregion
            }
            catch (Exception ex)
            {
                CatchMessage(ex);
                return Request.CreateResponse(HttpStatusCode.InternalServerError, "An unknown error has occurred.");
            }
        }


        #region Authenticate
        /// <summary>
        /// POST method validates user - Creats new user if the input user not available.
        /// </summary>
        /// <param name="loginModel"></param>
        /// <returns>HttpStatusCode</returns>
        [HttpPost]
        [Route("authenticate")]
        public HttpResponseMessage AuthenticateUsers([FromBody] LoginModel loginModel)
        {
            if (string.IsNullOrEmpty(loginModel.CreatedBy) || loginModel.CompanyId != 3 || loginModel.SugerGuestId == null || loginModel == null)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }
            try
            {
                LittleChapel.GuestAccount guestAccount =

                    hdc.GuestAccounts.Where(c => c.ContractId == loginModel.ContractId && c.Email == loginModel.EmailId && c.Deleted == false).FirstOrDefault();

                GuestResult guestResult = new GuestResult();
                if (guestAccount == null)
                {
                    //create guest
                    LittleChapel.GuestAccount guest = new LittleChapel.GuestAccount();
                    guest.ContractId = loginModel.ContractId;
                    guest.SugarCRMId = loginModel.SugerGuestId;
                    guest.CreatedDate = DateTime.Now;
                    guest.CreatedBy = loginModel.CreatedBy;
                    guest.CompanyId = loginModel.CompanyId;
                    guest.Email = loginModel.EmailId;
                    guest.FirstName = loginModel.FirstName;
                    guest.LastName = loginModel.LastName;
                    guest.Deleted = false;
                    hdc.GuestAccounts.InsertOnSubmit(guest);

                    hdc.SubmitChanges();

                    guestResult.GuestId = Convert.ToInt16(guest.GuestAccId);
                    guestResult.IsBooked = false;
                    guestResult.IsGuestHasAnyReservation = false;
                    return Request.CreateResponse(HttpStatusCode.OK, guestResult);
                }
                else
                {
                    LittleChapel.Reservation reservation = hdc.Reservations.Where(x => x.Deleted == false && x.CreatedBy == Convert.ToString(guestAccount.GuestAccId)).FirstOrDefault();
                    guestResult.GuestId = guestAccount.GuestAccId;
                    guestResult.IsBooked = true;
                    if (reservation != null)
                    {
                        guestResult.IsGuestHasAnyReservation = true;

                    }

                    else
                    {
                        guestResult.IsGuestHasAnyReservation = false;
                    }
                    return Request.CreateResponse(HttpStatusCode.OK, guestResult);
                }
            }
            catch (Exception ex)
            {
                CatchMessage(ex);
                return Request.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }
        #endregion


        #region PaymentSchedule GuestID
        /// <summary>
        /// Payment Schedule details based on salesorder 
        /// </summary>
        /// <param name="GuestID">GuestID</param>
        /// <param name="ContractID">ContractID</param>
        /// <returns>Payment Schedule and Past Payment details</returns>
        [HttpGet]
        [Route("PaymentScheduleList/{GuestID}/{ContractID}")]
        public HttpResponseMessage PaySchedule(int GuestID, int ContractID)
        {
            var guest = hdc.GuestDetails.Where(c => c.GuestAccId == GuestID && c.ContractId == ContractID);
            var reservation = guest.Select(c => c.Reservation).Distinct();
            var linkedsalesorderids = reservation.Select(c => c.LinkedSalesOrderId).ToList();
            PaymentScheduleResponse paymentscheduleresponse = new PaymentScheduleResponse();
            List<Salesorder> Salesorder = new List<Models.Salesorder>();
            Salesorder so;
            foreach (var l in linkedsalesorderids)
            {
                if (l != null)
                {
                    so = new Salesorder();
                    var linkedsalesorder = wms.SalesOrders.Where(c => c.SalesOrderID == l);
                    var linkedsalesobj = linkedsalesorder.Select(c => new { c.TotalPrice, c.TotalPaid, c.PlanID, c.SalesOrderID, c.CreateDate, c.BalanceDue }).FirstOrDefault();
                    so.PlanID = linkedsalesobj.PlanID;
                    so.SalesOrderID = linkedsalesobj.SalesOrderID;
                    so.BookedDate = linkedsalesobj.CreateDate;
                    so.BalanceDue = linkedsalesobj.BalanceDue;
                    var TotalRemaining = so.BalanceDue;
                    var Paymentschedules = linkedsalesorder.SelectMany(c => c.PaymentSchedules);
                    var EMI = Paymentschedules.Count();
                    LittleChapel.Payment newpayment = new LittleChapel.Payment();
                    if (EMI > 0)
                    {
                        so.paymentschedule = Paymentschedules.Select(c => new Paymentschedule { Paymentdate = c.PaymentDate, Amount = Math.Round(TotalRemaining / EMI, 2), Paytype = "Scheduled Invoices", Status = c.PaymentID == null ? PaymentStatus.Pending : c.Payment.PaymentStatus, Fee = newpayment.GenerateFee(Math.Round(TotalRemaining / EMI, 2)), TotalAmount = Math.Round(TotalRemaining / EMI, 2) + newpayment.GenerateFee(Math.Round(TotalRemaining / EMI, 2)) }).ToList();
                        Salesorder.Add(so);
                    }
                }
            }
            paymentscheduleresponse.salesorder = Salesorder;
            Logging(Log4net);
            return Request.CreateResponse(HttpStatusCode.OK, paymentscheduleresponse);
        }
        #endregion


        #region EMIDates
        private List<DateTime> GetDatesBetween(DateTime startDate, DateTime endDate, int monthcount)
        {
            var dates = new List<DateTime>();

            if (monthcount > 6)
            {
                int interval = monthcount - 6;
                for (var dt = startDate.AddMonths(1); dt <= endDate.AddMonths(-interval); dt = dt.AddMonths(1))
                {
                    dates.Add(dt);
                }

            }
            else
            {
                for (var dt = startDate.AddMonths(1); dt <= endDate; dt = dt.AddMonths(1))
                {
                    dates.Add(dt);
                }
            }
            return dates;
        }
        #endregion
        [HttpPost]
        [Route("DeletePaymentCard/{PaymentCardID}")]
        public HttpResponseMessage DeletePaymentCard(int PaymentCardID)
        {
            try
            {
                var paymentcard = wms.PaymentCards.Where(c => c.PaymentCardID == PaymentCardID).FirstOrDefault();
                paymentcard.Deleted = true;
                wms.SubmitChanges();
                return Request.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        #region Create Payment Schedule
        private void createPaymentSchedule(int PaymentCardID, SalesOrder so, DateTime FinalPaymentDate)
        {
            #region PaymentSchedule

            var schedule = new PaymentSchedule
            {
                PaymentCardID = PaymentCardID,
                PaymentDate = FinalPaymentDate,
                IsEmailed = false,
                SalesOrderID = so.SalesOrderID
            };
            wms.PaymentSchedules.InsertOnSubmit(schedule);
            #endregion
            #region Create note
            wms.Notes.InsertOnSubmit(new Note
            {
                NoteDate = DateTime.Now,
                IsActive = true,
                EmployeeID = 362,
                PlanID = schedule.PlanID,
                IsFrozen = false,
                IsReminder = false,
                Notes = "Added scheduled payment for: " + schedule.PaymentDate.ToString("MM/dd/yyyy"),
                SalesOrderID = schedule.SalesOrderID,
            });
            #endregion
            wms.SubmitChanges();
        }
        #endregion


        #region Store Payment Card
        private PaymentCard GetPaymentCard(string Token, SalesOrder so)
        {
            PaymentCard storedCard;

            //Get the card.  Look in PaymentCard first.  If not there, then it came from an admin payment.
            storedCard = wms.PaymentCards.SingleOrDefault(c => c.Token == Token && c.CustomerID == so.CustomerID.Value);

            if (storedCard == null)
            {
                //Came from payment
                var payment = wms.Payments.Where(c => c.SalesOrderID == so.SalesOrderID && c.Token == Token).OrderBy(c => c.PayDate).ToArray().LastOrDefault();

                if (payment == null)
                {
                    return null;
                }

                storedCard = new PaymentCard
                {
                    CustomerID = so.CustomerID.Value,
                    Token = Token,
                    ExpMo = Payment.FormatMonth(payment.CardMo),
                    ExpYr = Payment.FormatYear(payment.CardYr),
                    PayType = 5,
                    LastFour = payment.CardNumberMasked.Substring(payment.CardNumberMasked.Length - 4),
                    Zip = payment.CardZip
                };

                wms.PaymentCards.InsertOnSubmit(storedCard);
                wms.SubmitChanges();
            }
            return storedCard;
        }
        #endregion


        private string GetMaskedCardNumber(string CardNumber)
        {
            try
            {
                CardNumber = CardNumber.Replace(" ", "");
                string lastFour = CardNumber.Substring(CardNumber.Length - 4, 4);

                return "************" + lastFour;
            }
            catch
            {
                return "XX**********XXXX";
            }
        }

        /// <summary>
        /// Get the Payment Card Details
        /// </summary>
        /// <param name="GuestID"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("PaymentCardDetails/{GuestID}")]
        public HttpResponseMessage PaymentcardDetails(int GuestID)
        {
            try
            {
                PaymentCardResponse paymentcardresponse = new PaymentCardResponse();
                var guest = hdc.GuestDetails.Where(c => c.GuestAccId == GuestID);
                var reservation = guest.Select(c => c.Reservation).Distinct();
                var linkedsalesorderids = reservation.Select(c => c.LinkedSalesOrderId).ToList();
                List<PaymentCardDetails> paymentcarddetails = new List<PaymentCardDetails>();
                PaymentCardDetails pc;

                foreach (var l in linkedsalesorderids)
                {
                    var payments = wms.Payments.Where(c => c.SalesOrderID == l).Select(x => new { x.Token }).Distinct().ToList();
                    string cardname = wms.Payments.Where(c => c.SalesOrderID == l).Select(c => c.CardName).FirstOrDefault();
                    int Paytype = wms.Payments.Where(c => c.SalesOrderID == l).Select(c => c.PayType).FirstOrDefault();
                    foreach (var p in payments)
                    {
                        var paymentcard = wms.PaymentCards.Where(c => c.Token == p.Token && (c.Deleted == false || c.Deleted == null)).FirstOrDefault();
                        pc = new PaymentCardDetails();
                        pc.PayType = Paytype;
                        pc.CardName = cardname;
                        pc.Digits = paymentcard.LastFour;
                        pc.PaymentCardID = paymentcard.PaymentCardID;
                        //pc.Token = paymentcard.Token;
                        paymentcarddetails.Add(pc);

                    }
                }
                paymentcarddetails = paymentcarddetails.DistinctBy(c => c.PaymentCardID).ToList();
                paymentcardresponse.paymentcarddetails = paymentcarddetails;
                Logging(Log4net);
                return Request.CreateResponse(HttpStatusCode.OK, paymentcardresponse);
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }


        [HttpPut]
        [Route("DummyReservation")]
        public HttpResponseMessage DummyReservation(ReservationList reservationlist)
        {

            foreach (var d in reservationlist.reserve)
            {
                Reserve reserve = d;
                if (string.IsNullOrEmpty(reserve.ActionType))
                {
                    reserve.ActionType = "Create";
                }
                Log4net.Info("Entered Reservation Function.");
                HttpResponseMessage ValidationResponse = Check(reserve.reservation.CreatedBy, reserve.reservation.CompanyId, false);
                Log4net.Info("Validation Response:" + ValidationResponse);
                if (ValidationResponse.IsSuccessStatusCode)
                {
                    using (var scope = new TransactionScope())
                    {
                        try
                        {
                            reserve.reservation.CreatedDate = DateTime.Now;
                            reserve.reservation.IsValid = true;
                            int ReservationId = CreateReservation(reserve.reservation);
                            Log4net.Info("Reservation ID:" + ReservationId);
                            if (ReservationId > 0 && reserve != null && reserve.reservation != null && reserve.reservation.Rooms > 0)
                            {
                                int roomBookedId = 0;
                                int guestdetailResponseId = 0;
                                for (int RoomsBooked = 0; RoomsBooked < reserve.reservation.Rooms; RoomsBooked++)
                                {
                                    reserve.roombooked[RoomsBooked].ReservationId = ReservationId;
                                    reserve.roombooked[RoomsBooked].CreatedBy = reserve.reservation.CreatedBy;
                                    reserve.roombooked[RoomsBooked].CreatedDate = reserve.reservation.CreatedDate;
                                    reserve.roombooked[RoomsBooked].CompanyId = reserve.reservation.CompanyId;
                                    reserve.roombooked[RoomsBooked].IsValid = true;
                                    roomBookedId = RoomBooked(reserve.roombooked[RoomsBooked]);
                                    Log4net.Info("RoomBooked ID:" + roomBookedId);
                                    if (roomBookedId > 0)
                                    {
                                        for (int intI = 0; intI < reserve.roombooked[RoomsBooked].GuestDetails.Count(); intI++)
                                        {
                                            reserve.roombooked[RoomsBooked].GuestDetails[intI].ReservationId = ReservationId;
                                            reserve.roombooked[RoomsBooked].GuestDetails[intI].RoomBookedId = roomBookedId;
                                            reserve.roombooked[RoomsBooked].GuestDetails[intI].ContractId = reserve.reservation.ContractId;
                                            reserve.roombooked[RoomsBooked].GuestDetails[intI].CreatedBy = reserve.reservation.CreatedBy;
                                            reserve.roombooked[RoomsBooked].GuestDetails[intI].CreatedDate = reserve.reservation.CreatedDate;
                                            reserve.roombooked[RoomsBooked].GuestDetails[intI].CompanyId = reserve.reservation.CompanyId;
                                            reserve.roombooked[RoomsBooked].GuestDetails[intI].GuestAccId = Convert.ToInt32(reserve.reservation.CreatedBy);
                                            reserve.roombooked[RoomsBooked].GuestDetails[intI].IsValid = true;
                                            guestdetailResponseId = GuestDetail(reserve.roombooked[RoomsBooked].GuestDetails[intI]);
                                            Log4net.Info("GuestDetail ID:" + guestdetailResponseId);
                                            if (guestdetailResponseId == 0)
                                            {
                                                Logging(Log4net);
                                                return CatchMessage(null);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Logging(Log4net);
                                        return CatchMessage(null);
                                    }
                                }
                                if (ReservationId > 0 && roomBookedId > 0 && guestdetailResponseId > 0)
                                {
                                    Models.RoomInventory roomInventory = new Models.RoomInventory();
                                    roomInventory.CompanyId = reserve.reservation.CompanyId;
                                    roomInventory.RoomCatId = reserve.roombooked[0].RoomCatId;
                                    roomInventory.TotalHeld = reserve.reservation.Rooms;
                                    roomInventory.ContractId = reserve.reservation.ContractId;
                                    roomInventory.CreatedBy = reserve.reservation.CreatedBy;
                                    roomInventory.CreatedDate = reserve.reservation.CreatedDate;
                                    roomInventory.TotalPartiallySold = 0;
                                    roomInventory.TotalSold = 0;
                                    roomInventory.TotalHold = true;
                                    int RoomInventoryId = RoomInvCommon(ReservationId, roomInventory);
                                    if (RoomInventoryId > 0)
                                    {
                                        scope.Complete();
                                        ReservationResponse reserveResponse = new ReservationResponse();
                                        reserveResponse.ReservationID = ReservationId;
                                        reserveResponse.RoomInventoryID = RoomInventoryId;
                                        //return Request.CreateResponse(HttpStatusCode.OK, reserveResponse);
                                    }
                                    else
                                    {
                                        Logging(Log4net);
                                        return CatchMessage(null);
                                    }
                                }
                                else
                                {
                                    Logging(Log4net);
                                    return Request.CreateResponse(HttpStatusCode.InternalServerError, "One or all status codes failed.");
                                }
                            }
                            else
                            {
                                Logging(Log4net);
                                return CatchMessage(null);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logging(Log4net);
                            return CatchMessage(ex);
                        }
                    }
                }
                else
                {
                    Logging(Log4net);
                    return ValidationResponse;
                }
            }

            return Request.CreateResponse(HttpStatusCode.OK);

        }



        [HttpPut]
        [Route("DummyAuthenticate")]
        public HttpResponseMessage DummyAuthenticateGuest(DummyAuthenticate dummyauthenticate)
        {
            foreach (var l in dummyauthenticate.loginmodel)
            {
                LoginModel loginModel = l;
                if (string.IsNullOrEmpty(loginModel.CreatedBy) || loginModel.CompanyId != 3 || loginModel.SugerGuestId == null || loginModel == null)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest);
                }
                try
                {
                    LittleChapel.GuestAccount guestAccount =

                        hdc.GuestAccounts.Where(c => c.ContractId == loginModel.ContractId && c.Email == loginModel.EmailId && c.Deleted == false).FirstOrDefault();

                    GuestResult guestResult = new GuestResult();
                    if (guestAccount == null)
                    {
                        //create guest
                        LittleChapel.GuestAccount guest = new LittleChapel.GuestAccount();
                        guest.ContractId = loginModel.ContractId;
                        guest.SugarCRMId = loginModel.SugerGuestId;
                        guest.CreatedDate = DateTime.Now;
                        guest.CreatedBy = loginModel.CreatedBy;
                        guest.CompanyId = loginModel.CompanyId;
                        guest.Email = loginModel.EmailId;
                        guest.FirstName = loginModel.FirstName;
                        guest.LastName = loginModel.LastName;
                        guest.Deleted = false;
                        hdc.GuestAccounts.InsertOnSubmit(guest);

                        hdc.SubmitChanges();

                        //guestResult.GuestId = Convert.ToInt16(guest.GuestAccId);
                        //guestResult.IsBooked = false;
                        //guestResult.IsGuestHasAnyReservation = false;
                        //return Request.CreateResponse(HttpStatusCode.OK, guestResult);
                    }


                }

                catch (Exception ex)
                {
                    CatchMessage(ex);
                    return Request.CreateResponse(HttpStatusCode.InternalServerError);
                }
            }

            return Request.CreateResponse(HttpStatusCode.OK);
        }

        [HttpPut]
        [Route("DummyDepositGet")]
        public HttpResponseMessage DummyDepositPay(DummyBillingInfo dummybillinginfo)
        {
            var GuestAccountInfo = hdc.GuestAccounts.Where(c => c.Email == dummybillinginfo.Email).SingleOrDefault();
            var Reservationid = hdc.GuestDetails.Where(c => c.GuestAccId == Convert.ToInt32(GuestAccountInfo.GuestAccId) && c.Deleted == false || c.Deleted == null).Select(x => new { x.ReservationId }).FirstOrDefault();


            Customer newcustomer = new Customer();
            newcustomer.BrideNameFirst = string.Empty;
            newcustomer.BrideNameLast = string.Empty;
            newcustomer.GroomNameFirst = GuestAccountInfo.FirstName;
            newcustomer.GroomNameLast = GuestAccountInfo.LastName;
            newcustomer.CompanyID = GuestAccountInfo.CompanyId;
            newcustomer.CustomerType = 2;

            string source = dummybillinginfo.scheduledetail.userinfo.DefaultAddress;
            string[] stringSeparators = new string[] { "<br/>" };
            string[] add = source.Split(stringSeparators, StringSplitOptions.None);

            Address newaddress = new Address();
            //newaddress.Street = add[0] + "" + add[1];
            //newaddress.Street2 = string.Empty;
            //newaddress.Unit = string.Empty;
            //newaddress.City = add[3];
            //newaddress.Country = add[4];
            newaddress.Street = "";
            newaddress.Street2 = "";
            newaddress.Unit = "";
            newaddress.City = "";
            newaddress.Country = "";
            wms.Addresses.InsertOnSubmit(newaddress);
            wms.SubmitChanges();

            newcustomer.BillingAddressID = newaddress.AddressID;
            newcustomer.ShippingAddressID = newaddress.AddressID;
            newcustomer.DayPhone = (string.IsNullOrEmpty(GuestAccountInfo.PhoneNo) ? string.Empty : GuestAccountInfo.PhoneNo);
            newcustomer.EvePhone = (string.IsNullOrEmpty(GuestAccountInfo.PhoneNo) ? string.Empty : GuestAccountInfo.PhoneNo);
            newcustomer.Fax = string.Empty;
            newcustomer.IsEmailOptedOut = false;
            newcustomer.ReferralID = 0;
            newcustomer.AffiliateID = 0;
            newcustomer.AuthUserID = 0;
            newcustomer.IsRenewal = false;
            if (GuestAccountInfo.Email != "")
            {
                newcustomer.Email = GuestAccountInfo.Email;
            }
            wms.Customers.InsertOnSubmit(newcustomer);
            wms.SubmitChanges();

            var contract = hdc.Contracts.Where(c => c.ContractId == GuestAccountInfo.ContractId).SingleOrDefault();
            var eventdetail = hdc.EventDetails.Where(c => c.EventID == contract.EventID).SingleOrDefault();

            SalesOrder so = new SalesOrder();
            so.CustomerID = newcustomer.CustomerID;
            so.PlanID = eventdetail.Planid;
            so.OrderDate = DateTime.Now;
            so.CreateDate = DateTime.Now;
            so.CreateID = 0;
            so.TotalPrice = dummybillinginfo.scheduledetail.total;
            so.TotalDue = dummybillinginfo.scheduledetail.balancedue;
            wms.SalesOrders.InsertOnSubmit(so);
            wms.SubmitChanges();

            LittleChapel.Reservation Reservation = hdc.Reservations.Where(c => c.ReservationId == Convert.ToInt32(Reservationid) && c.Status == "ON HOLD" || c.Status == "CONFIRMED" && c.Deleted == false || c.Deleted.Equals(null)).SingleOrDefault();
            Reservation.Status = "CONFIRMED";
            Reservation.TotalPrice = dummybillinginfo.scheduledetail.total;
            Reservation.LinkedSalesOrderId = so.SalesOrderID;
            hdc.SubmitChanges();

            Models.RoomInventory roomInventories = new Models.RoomInventory();
            roomInventories.ModifiedBy = GuestAccountInfo.CreatedBy;
            roomInventories.TotalSeld = true;
            RoomInvCommon(Convert.ToInt32(Reservationid), roomInventories);

            return Request.CreateResponse(HttpStatusCode.OK);
        }





    }
}


