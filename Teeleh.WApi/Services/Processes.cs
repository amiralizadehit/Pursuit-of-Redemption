﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Data.Entity;
using Teeleh.Models;
using Teeleh.Models.Enums;
using Teeleh.Utilities;
using Teeleh.Utilities.Notification_Object;

namespace Teeleh.WApi.Services
{
    public class Processes
    {
        private AppDbContext db;
        public Processes()
        {
            db = new AppDbContext();
        }

        public void SendNotification()
        {
            // We just resend the notifications which have not been seen before by its target user
            var notifications = db.Notifications.Where(n => n.Status == NotificationStatus.UNSEEN);


            if (notifications.Any())
            {
                foreach (var notification in notifications)
                {
                    var userSessions = notification.User.Sessions;
                
                    // we send the notification to all active sessions of its target user
                    foreach (var userSession in userSessions)
                    {
                        //we want to make sure that we send notification to an active session
                        if (userSession.State == SessionState.ACTIVE)
                        {
                            var fcmToken = userSession.FCMToken;
                            if (fcmToken != null)
                            {
                                // Here we create the notification Object based on the type of notification
                                NotificationObject notificationObject;

                                switch (notification.Type)
                                {
                                    case NotificationType.ADVERTISEMENT :
                                        var game = db.Games.Single(g => g.Id == notification.Advertisement.GameId);
                                        notificationObject = new AdNotification()
                                        {
                                            Id = notification.Id,
                                            Avatar = game.Avatar.ImagePath,
                                            AdvertisementId = notification.AdvertisementId,
                                            Message = notification.Message,
                                            //type = NotificationType.ADVERTISEMENT
                                        };
                                        NotificationSender.SendRequestNotification(fcmToken, notificationObject);
                                        break;
                                    case NotificationType.CASUAL :
                                        notificationObject = new CasualNotification()
                                        {
                                            Id = notification.Id,
                                            //Todo: Here we should add a default image for our casual notifications.
                                            Avatar = "",
                                            Message = notification.Message,
                                           // type = NotificationType.CASUAL
                                        };
                         
                                        break;
                                }
                            }
                        }
                    }
                
                }
            }
        }

        public void UpdateRecommenderSystem()
        {



            //var advertisementInDb = await db.Advertisements.Where(QueryHelper.GetAdvertisementValidationQuery())
            //    .SingleOrDefaultAsync(c => c.Id == id);
            //if (advertisementInDb != null)
            //{
            //    var game = advertisementInDb.Game;
            //    var toQuery = db.Advertisements.Where(QueryHelper.GetAdvertisementValidationQuery())
            //        .Where(a => a.Id != id);
            //    var similarAds = toQuery.Where(a => a.Game.Id == game.Id).Take(10);
            //    if (similarAds.Count() < 4)
            //    {
            //        var numLeft = 4 - similarAds.Count();
            //        var toAdd = toQuery.Where(a => a.LocationCityId == advertisementInDb.LocationCityId).Take(numLeft);
            //        similarAds = similarAds.Concat(toAdd);
            //        if (toAdd.Count() < numLeft)
            //        {
            //            var numLeft2 = numLeft - toAdd.Count();
            //            var toAdd2 = toQuery.Where(a => a.Game.Developer == game.Developer).Take(numLeft2);
            //            similarAds = similarAds.Concat(toAdd2);
            //        }
            //    }

            //    var result = similarAds.Select(QueryHelper.GetAdvertisementQuery()).ToList();
                
            //}
        }

    }
}