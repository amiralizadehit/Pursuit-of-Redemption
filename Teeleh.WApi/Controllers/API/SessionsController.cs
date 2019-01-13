﻿using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using Teeleh.Models;
using Teeleh.Models.ViewModels;
using Teeleh.WApi.Helper;

namespace Teeleh.WApi.Controllers
{
    /// <summary>
    /// This class is used to manage sessions and authentication
    /// </summary>
    public class SessionsController : ApiController
    {
        AppDbContext db = new AppDbContext();


        /// <summary>
        /// Returns a list of all sessions.
        /// </summary>
        /// <returns>List of all sessions</returns>
        [HttpGet]
        public IHttpActionResult GetSessions()
        {

            return Ok(db.Sessions.Select(QueryHelper.GetSessionQuery()).ToList());
        }


        /// <summary>
        /// Actives created session for user with given phoneNumber and nonce then returns SessionId and SessionKey.
        /// </summary>
        /// <returns>200 : Ok (User Confirmed Successfully - Session Info Sent) |
        /// 404 : No Session Found (Wrong Nounce Entered) |
        /// 400 : Bad Request 
        /// </returns>
        [HttpPut]
        [Route("api/sessions/active")]
        public async Task<IHttpActionResult> Active(PendingSessionViewModel pendingSession)
        {
            if (ModelState.IsValid)
            {
                var session =
                    await db
                        .Sessions
                        .Where(QueryHelper.GetPendingSessionQuery(pendingSession))
                        .SingleOrDefaultAsync();
                if (session == null)
                {
                    return NotFound(); //Wrong Nounce Entered
                }

                session.User.State = SessionState.Active;
                session.User.UpdatedAt = DateTime.Now;
                session.ActivationMoment = DateTime.Now;
                session.State = SessionState.Active;

                await db.SaveChangesAsync();

                SessionInfoObject sessionInfo = new SessionInfoObject()
                {
                    SessionKey = session.SessionKey,
                    SessionId = session.Id
                };

                return Ok(sessionInfo);
            }

            return BadRequest();
        }

        /// <summary>
        /// Deactives active session with given SessionId and SessionKey - User Logout
        /// </summary>
        /// <returns>200 : Ok (Session Deactivated Successfully) |
        /// 404 : No Session Found (Wrong SessionId or SessionKey) |
        /// 400 : Bad Request 
        /// </returns>
        [HttpPut]
        [Route("api/sessions/deactive")]
        public async Task<IHttpActionResult> Deactive(SessionInfoObject sessionInfo)
        {
            if (ModelState.IsValid)
            {
                var session =
                    await db
                        .Sessions
                        .Where(QueryHelper.GetSessionValidationQuery(sessionInfo))
                        .SingleOrDefaultAsync();
                if (session == null)
                {
                    return NotFound();
                }

                session.DeactivationMoment = DateTime.Now;
                session.State = SessionState.Deactivate;
                await db.SaveChangesAsync();

                return Ok();
            }

            return BadRequest();
        }
    }
}