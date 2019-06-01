﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using Teeleh.Models;
using Teeleh.Models.Dtos;
using Teeleh.Models.Enums;
using Teeleh.Models.Helper;
using Teeleh.Utilities;


namespace Teeleh.WApi.Functions
{
    /// <summary>
    /// This static class is responsible for making new notifications for the users.
    /// </summary>
    public class NotificationGenerator
    {
        /// <summary>
        /// This helper function broadcasts newly-created advertisements
        /// </summary>
        public static void NewAdvertisementNotification(AppDbContext db, Advertisement advertisement)
        {
            //Here we get all the requests matching this advertisement
            var requests = GetMatchedRequests(db, advertisement);

            if (!requests.Any()) return; //we don't have any request matching this advertisement.
            foreach (var request in requests)
            {
                var requestUser = request.User;
                var advertisementUser = advertisement.User;

               //Here we have to make sure that advertisement creator and request creator are not the same.
                if (requestUser.Id != advertisementUser.Id)
                {
                    //No one has received this notification before
                    var newNotification = new Notification()
                    {
                        //AdvertisementId = advertisement.Id,
                        CreatedAt = DateTime.Now,
                        Title = "Teeleh",
                        UserId = requestUser.Id,
                        Message = "آگهی مورد نظر شما موجود شد",
                        Status = NotificationStatus.UNSEEN,
                        Type = NotificationType.ADVERTISEMENT
                    };
                    db.Notifications.Add(newNotification);
                }
            }

            db.SaveChanges();
        }


        /// <summary>
        /// This helper function broadcasts edited advertisements.
        /// </summary>
        public static void OldAdvertisementNotification(AppDbContext db, Advertisement advertisement,
            bool resend = false)
        {
            //Here we get all the requests matching this advertisement
            var requests = GetMatchedRequests(db, advertisement);

            if (!requests.Any()) return; //we don't have any request matching this advertisement.

            foreach (var request in requests)
            {
                var user = request.User;

                var hasReceived = false;
                //we should check if a user has already received a notification for this advertisement
                foreach (var notification in user.Notifications)
                {
                    //if (notification.AdvertisementId == advertisement.Id
                    //) //this notification has been sent before to this user
                    //{
                    //    hasReceived = true;
                    //    if (resend)
                    //        here we resend the notification again.
                    //        notification.Status = NotificationStatus.UNSEEN;
                    //    break;
                    //}
                }

                if (!hasReceived)
                {
                    //this is the first time user gets this notification

                    //Here we have to make sure that advertisement creator and request creator are not the same.
                    var requestUser = request.User;
                    var advertisementUser = advertisement.User;

                    if (requestUser.Id != advertisementUser.Id)
                    {
                        var newNotification = new Notification()
                        {
                            //AdvertisementId = advertisement.Id,
                            AvatarId = advertisement.Game.Avatar.Id,
                            CreatedAt = DateTime.Now,
                            Title = "Teeleh",
                            UserId = user.Id,
                            Message = "آگهی انتخابی شما تخفیف خورده است",
                            Status = NotificationStatus.UNSEEN,
                            Type = NotificationType.ADVERTISEMENT
                        };
                        db.Notifications.Add(newNotification);
                    }
                }
            }
            db.SaveChanges();
        }

        /// <summary>
        /// This helper function send casual notification to all active users.
        /// </summary>
        /// <param name="db"></param>
        public static void CasualNotification(AppDbContext db, string title, string message, Image avatar)
        {
            var users = db.Users.Where(QueryHelper.GetUserValidationQuery());
            foreach (var user in users)
            {
                var newNotification = new Notification()
                {
                    CreatedAt = DateTime.Now,
                    UserId = user.Id,
                    Message = message,
                    Title = title,
                    Avatar = avatar,
                    Status = NotificationStatus.UNSEEN,
                    Type = NotificationType.CASUAL,
                };
                db.Notifications.Add(newNotification);
            }
            db.SaveChanges();
        }


        /// <summary>
        /// This helper function returns all the requests that match this advertisement.
        /// </summary>
        private static IQueryable<Request> GetMatchedRequests(AppDbContext db, Advertisement advertisement)
        {
            var adPrice = advertisement.Price;
            var exchangeGamesCounts = advertisement.ExchangeGames?.Count ?? 0;

            var query = db.Requests.Where(QueryHelper.GetRequestValidationQuery())
                .Where(s => s.GameId == advertisement.GameId).Include(k => k.User.Notifications);

            if (adPrice != 0 && exchangeGamesCounts > 0)
            {
                query = query.Where(r => r.ReqMode == RequestMode.ALL)
                    .Where(p => p.MinPrice <= adPrice && p.MaxPrice >= adPrice);
            }
            else if (adPrice == 0 && exchangeGamesCounts > 0)
            {
                query = query.Where(r => r.ReqMode == RequestMode.JUST_EXCHANGE);
            }
            else
            {
                query = query.Where(r => r.ReqMode == RequestMode.JUST_SELL)
                    .Where(p => p.MinPrice <= adPrice && p.MaxPrice >= adPrice);
            }

            query = (MediaType) advertisement.MedType == MediaType.NEW
                ? query.Where(r => r.FilterType == FilterType.JUST_NEW)
                : query.Where(r => r.FilterType == FilterType.JUST_SECOND_HAND);

            return query;
        }
    }
}