﻿using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using Teeleh.Models;
using Teeleh.Models.Dtos;
using Teeleh.Models.Enums;
using Teeleh.Models.Helper;
using Teeleh.Models.ViewModels;
using Teeleh.Utilities;

namespace Teeleh.WApi.Controllers
{
    /// <summary>
    /// This class is used to manage client requests related to users.
    /// </summary>
    public class UsersController : ApiController
    {
        private AppDbContext db;

        public UsersController()
        {
            db = new AppDbContext();
        }


        /// <summary>
        /// Returns a list of all users.
        /// </summary>
        /// <returns>List of users</returns>
        [HttpGet]
        public IHttpActionResult GetUsers()
        {
            
            return Ok(db.Users.Select(QueryHelper.GetUserQuery()).ToList());
        }


        

        ////////////////////////// User Login /////////////////////////////
        /// <summary>
        /// It is used for user login.
        /// </summary>
        /// <returns>200 : Ok (User Logged in Successfully - Session Info Sent) |
        /// 409 : Not Confirmed User |
        /// 400 : Bad Request |
        /// 404 : Not Registered User
        /// </returns>
        [HttpPost]
        [Route("api/users/login")]
        public async Task<IHttpActionResult> Login(LoginViewModel loginInfo)
        {
            if (ModelState.IsValid)
            {
                User user = null;
                var hashPassword = HasherHelper.sha256_hash(loginInfo.Password);
                if (!string.IsNullOrEmpty(loginInfo.Email))
                {
                    
                    user = await db.Users.SingleOrDefaultAsync(q =>
                        q.Email == loginInfo.Email && q.Password == hashPassword);
                }
                else if (!string.IsNullOrEmpty(loginInfo.PhoneNumber))
                {
                    user = await db.Users.SingleOrDefaultAsync(q =>
                        q.PhoneNumber == loginInfo.PhoneNumber && q.Password == hashPassword);
                }
                else
                {
                    return BadRequest();
                }
               


                if (user == null) //Not Registered User - Use Sign up form
                {
                    return NotFound();
                }

                if (user.State == SessionState.ACTIVE)
                {
                    var sessionKey = RandomHelper.RandomString(32);

                    Session newSession = new Session()
                    {
                        Nonce = null,
                        State = SessionState.ACTIVE,
                        InitMoment = DateTime.Now,
                        ActivationMoment = DateTime.Now,
                        SessionKey = sessionKey,
                        FCMToken = loginInfo.FCMToken,
                        SessionPlatform = (SessionPlatform)loginInfo.SessionPlatform,
                        UniqueCode = loginInfo.UniqueCode,
                        User = user
                    };
                    db.Sessions.Add(newSession);
                    

                    await db.SaveChangesAsync();

                    SessionInfoObject sessionIfInfoObject = new SessionInfoObject()
                    {
                        SessionKey = sessionKey,
                        SessionId = newSession.Id,
                    };

                    //OK
                    return Ok(sessionIfInfoObject);
                }

                return Conflict(); //Not confirmed user
            }

            return BadRequest();
        }


        /////////////////////////  User SignUp ////////////////////////////

        /// <summary>
        /// It is used for user sign up with given information. (uses SMS/Email for Two-Factor authentication) 
        /// </summary>
        /// <returns>200 : Ok (User Created - SMS Sent - SessionId Returned) |
        /// 500 : Internal Server Error (SMS Not Sent) |
        /// 400 : Bad Request |
        /// 409 : Already Registered User
        /// </returns>
        [HttpPost]
        [Route("api/users/signup")]
        public async Task<IHttpActionResult> SignUp(UserSignUpViewModel userSignUp)
        {
            if (ModelState.IsValid)
            {
                User user = null;

                if(!string.IsNullOrEmpty(userSignUp.PhoneNumber))
                    user = await db.Users.SingleOrDefaultAsync(q => q.PhoneNumber == userSignUp.PhoneNumber);
                else if (!string.IsNullOrEmpty(userSignUp.Email))
                    user = await db.Users.SingleOrDefaultAsync(q => q.Email == userSignUp.Email);
                else
                    return BadRequest();

                Session session = null;
                var randomNounce = RandomHelper.RandomInt(10000, 99999);
                
                if (user == null) //New User
                {
                    user = new User()
                    {
                        FirstName = userSignUp.FirstName,
                        LastName = userSignUp.LastName,
                        PhoneNumber = userSignUp.PhoneNumber,
                        Email = userSignUp.Email,
                        Password = HasherHelper.sha256_hash(userSignUp.Password),
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
                        State = SessionState.PENDING
                    };

                    db.Users.Add(user);

                    session = new Session()
                    {
                        Nonce = randomNounce,
                        State = SessionState.PENDING,
                        InitMoment = DateTime.Now,
                        SessionKey = RandomHelper.RandomString(32),
                        FCMToken = userSignUp.FCMToken,
                        SessionPlatform = (SessionPlatform)userSignUp.SessionPlatform,
                        UniqueCode = userSignUp.UniqueCode,
                        User = user
                    };

                    db.Sessions.Add(session);
                    await db.SaveChangesAsync();

                }
                else
                {
                    if (user.State == SessionState.PENDING) //multiple requests
                    {
                        db.Sessions
                            .Where(q => q.UniqueCode == userSignUp.UniqueCode)
                            .Where(q => q.State == SessionState.PENDING)
                            .ToList()
                            .ForEach(q => q.State = SessionState.ABOLISHED);

                        session = new Session()
                        {
                            Nonce = randomNounce,
                            State = SessionState.PENDING,
                            InitMoment = DateTime.Now,
                            SessionKey = RandomHelper.RandomString(32),
                            FCMToken = userSignUp.FCMToken,
                            SessionPlatform = (SessionPlatform)userSignUp.SessionPlatform,
                            UniqueCode = userSignUp.UniqueCode,
                            User = user
                        };

                        db.Sessions.Add(session);

                        await db.SaveChangesAsync();

                    }
                    else if (user.State == SessionState.ACTIVE) //already registered user - use login form
                    {
                        return Conflict();
                    }
                }

                var receptorPhone = userSignUp.PhoneNumber;
                var receptorMail = userSignUp.Email;
                var token = randomNounce.ToString();

                if (receptorMail != null) //Mail
                {
                    MessageHelper.CodeVerificationEmail(token, receptorMail, MessageHelper.EmailMode.VERIFICATION);
                }

                if (receptorPhone!=null)
                {
                    if (MessageHelper.SendSMS_K(token, receptorPhone, MessageHelper.SMSMode.VERIFICATION) != null) //SMS
                    {
                        return InternalServerError();
                    }
                }

                return Json(new
                {
                    SessionId = session?.Id ?? -1
                });
            }

            return BadRequest();
        }


        /// <summary>
        /// It is used for user sign up with given information. (uses Email for Two-Factor authentication)
        /// </summary>
        /// <returns>200 : Ok (User Created - Email Sent - SessionId Returned) |
        /// 400 : Bad Request |
        /// 409 : Already Registered User
        /// </returns>
        [HttpPost]
        [Route("api/users/signup/email")]
        public async Task<IHttpActionResult> SignUp(UserSignUpEmailViewModel userSignUpEmail)
        {
            if (ModelState.IsValid)
            {
                User user = await db.Users.SingleOrDefaultAsync(q => q.Email == userSignUpEmail.Email);
                Session session = null;
                var randomNounce = RandomHelper.RandomInt(10000, 99999);

                if (user == null) //New User
                {
                    user = new User()
                    {
                        FirstName = userSignUpEmail.FirstName,
                        LastName = userSignUpEmail.LastName,
                        PhoneNumber = userSignUpEmail.PhoneNumber,
                        Email = userSignUpEmail.Email,
                        Password = HasherHelper.sha256_hash(userSignUpEmail.Password),
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
                        State = SessionState.PENDING
                    };

                    db.Users.Add(user);

                    session = new Session()
                    {
                        Nonce = randomNounce,
                        State = SessionState.PENDING,
                        InitMoment = DateTime.Now,
                        SessionKey = RandomHelper.RandomString(32),
                        FCMToken = userSignUpEmail.FCMToken,
                        SessionPlatform = (SessionPlatform)userSignUpEmail.SessionPlatform,
                        UniqueCode = userSignUpEmail.UniqueCode,
                        User = user
                    };

                    db.Sessions.Add(session);
                    await db.SaveChangesAsync();

                }
                else
                {
                    if (user.State == SessionState.PENDING) //multiple requests
                    {
                        db.Sessions
                            .Where(q => q.UniqueCode == userSignUpEmail.UniqueCode)
                            .Where(q => q.State == SessionState.PENDING)
                            .ToList()
                            .ForEach(q => q.State = SessionState.ABOLISHED);

                        session = new Session()
                        {
                            Nonce = randomNounce,
                            State = SessionState.PENDING,
                            InitMoment = DateTime.Now,
                            SessionKey = RandomHelper.RandomString(32),
                            FCMToken = userSignUpEmail.FCMToken,
                            SessionPlatform = (SessionPlatform)userSignUpEmail.SessionPlatform,
                            UniqueCode = userSignUpEmail.UniqueCode,
                            User = user
                        };

                        db.Sessions.Add(session);

                        await db.SaveChangesAsync();

                    }
                    else if (user.State == SessionState.ACTIVE) //already registered user - use login form
                    {
                        return Conflict();
                    }
                }

                var receptor = userSignUpEmail.Email;
                var token = randomNounce.ToString();
                MessageHelper.CodeVerificationEmail(token, receptor, MessageHelper.EmailMode.VERIFICATION);
                
                return Json(new
                {
                    SessionId = session?.Id ?? -1
                });
            }

            return BadRequest();
        }



        ////////////////////// User Forget Password ///////////////////////


        /// <summary>
        /// It is used to send password recovery code via SMS/Email to users who have forgotten their passwords with given phone number/Email.
        /// </summary>
        /// <param name="phoneNumber"></param>
        /// <returns>200 : Ok (SMS Sent) |
        /// 505 : Internal Server Error (SMS Not Sent) |
        /// 409 : Not Confirmed User |
        /// 404 : User Not Found |
        /// 400 : Bad Request
        /// </returns>
        [HttpGet]
        [Route("api/users/forgotpassword/{phoneOrMail}")]
        public async Task<IHttpActionResult> ForgotPassword(string phoneOrMail)
        {
            if (ModelState.IsValid)
            {
                User user = null;
                bool isEmail = false;
                if (phoneOrMail.Contains("@"))
                {
                     user = await db.Users.SingleOrDefaultAsync(q => q.Email == phoneOrMail);
                    isEmail = true;
                }
                else
                {
                     user = await db.Users.SingleOrDefaultAsync(q => q.PhoneNumber == phoneOrMail);
                    isEmail = false;
                }
                
                var randomNounce = RandomHelper.RandomInt(10000, 99999);

                if (user == null)
                {
                    return NotFound(); //user not found
                }

                if (user.State==SessionState.ACTIVE)
                {
                    var email = user.Email;
                    user.ForgetPassCode = randomNounce;

                    await db.SaveChangesAsync();

                    var receptor = phoneOrMail;
                    var token = randomNounce.ToString();

                    if (isEmail) //mail
                    {
                        MessageHelper.CodeVerificationEmail(token, email, MessageHelper.EmailMode.PASSWORD_RECOVERY);
                    }
                    else
                    {
                        if (MessageHelper.SendSMS_K(token, receptor,MessageHelper.SMSMode.PASSWORD_RECOVERY) != null) //SMS
                        {
                            return InternalServerError();
                        }
                    }

                    return Ok();
                }

                return Conflict(); //Not Confirmed User
            }

            return BadRequest();
        }



        /// <summary>
        /// It is used to send password recovery code via Email to users who have forgotten their passwords with given Email.
        /// </summary>
        /// <param name="email"></param>
        /// <returns>200 : Ok (Email Sent) |
        /// 404 : User Not Found |
        /// 409 : Not Confirmed User |
        /// 400 : Bad Request
        /// </returns>
        [HttpGet]
        [Route("api/users/forgotpassword/email/{email}")]
        public async Task<IHttpActionResult> ForgotPasswordEmail(string email)
        {
            if (ModelState.IsValid)
            {
                User user = await db.Users.SingleOrDefaultAsync(q => q.Email == email);
                var randomNounce = RandomHelper.RandomInt(10000, 99999);

                if (user == null)
                {
                    return NotFound();
                }

                if (user.State==SessionState.ACTIVE)
                {
                    user.ForgetPassCode = randomNounce;

                    await db.SaveChangesAsync();

                    var receptor = email;
                    var token = randomNounce.ToString();
                    MessageHelper.CodeVerificationEmail(token, receptor, MessageHelper.EmailMode.PASSWORD_RECOVERY);

                    return Ok();
                }

                return Conflict();
            }

            return BadRequest();
        }



        /// <summary>
        /// It is used to change the old password of users who have forgotten their passwords.
        /// </summary>
        /// <returns>200 : Ok (Password Changed Successfully) |
        /// 404 : User Not Found (Wrong Code Entered) |
        /// 400 : Bad Request
        /// </returns>
        [HttpPut]
        [Route("api/users/forgotpassword")]
        public async Task<IHttpActionResult> ForgotPasswordReset(
            ForgotPasswordVerificationViewModel forgotPasswordVerificationViewModel)
        {
            if (ModelState.IsValid)
            {
                User user = await db.Users.SingleOrDefaultAsync(q =>
                    q.ForgetPassCode == forgotPasswordVerificationViewModel.ForgetPassCode);
                if (user == null)
                {
                    return NotFound(); //Wrong Code
                }

                user.Password = HasherHelper.sha256_hash(forgotPasswordVerificationViewModel.Password);
                user.ForgetPassCode = 0;
                await db.SaveChangesAsync();

                return Ok();
            }

            return BadRequest();
        }


        /// <summary>
        /// It is used to get all active sessions of a user
        /// </summary>
        /// <returns>200 : Ok |
        /// 404 : No Session Found With Given SessionKey |
        /// 400 : Bad Request 
        /// </returns>
        [HttpPost]
        [Route("api/users/sessions/me")]
        public async Task<IHttpActionResult> GetUserSessions(SessionInfoObject sessionInfoObj)
        {
            if (ModelState.IsValid)
            {
                Session session = await db.Sessions.Include(u=>u.User).SingleOrDefaultAsync(QueryHelper.GetSessionValidationQuery(sessionInfoObj));

                if (session != null)
                {
                    User user = session.User;                    
                    return Ok(
                    
                        user.Sessions.Select(q => new
                        {
                            ActivationMoment = q.ActivationMoment,
                            DeactivationMoment = q.DeactivationMoment,
                            UniqueCode = q.UniqueCode,
                            State = q.State
                        }).ToList()
                    );
                }

                return NotFound();
            }

            return BadRequest();
        }


        /// <summary>
        /// This endpoint returns a list of advertisements owned by a user specified by given session information.
        /// </summary>
        /// <returns>200 : sent |
        /// 400 : Bad Request |
        /// 401 : Session info not found
        /// </returns>
        [HttpPost]
        [Route("api/users/advertisements/me")]
        public async Task<IHttpActionResult> GetAdvertisements(SessionInfoObject sessionInfoObject)
        {
            if (ModelState.IsValid)
            {
                var sessionInDb = await
                    db.Sessions.SingleOrDefaultAsync(QueryHelper.GetSessionValidationQuery(sessionInfoObject));
                if (sessionInDb != null)
                {
                    var user = sessionInDb.User;
                    var advertisement = db.Advertisements
                        .Where(QueryHelper.GetAdvertisementValidationQuery()).Where(c => c.User.Id == user.Id)
                        .Select(QueryHelper.GetAdvertisementQuery()).ToList();
                    return Ok(advertisement);
                }

                return Unauthorized();
            }

            return BadRequest();
        }



        /// <summary>
        /// This endpoint returns a list of advertisements saved by a user specified by given session information.
        /// </summary>
        /// <returns>200 : Sent |
        /// 400 : Bad Request |
        /// 401 : Session info not found
        /// </returns>
        [HttpPost]
        [Route("api/users/advertisements/bookmark/me")]
        public async Task<IHttpActionResult> GetBookmarkedAdvertisements(SessionInfoObject sessionInfoObject)
        {
            if (ModelState.IsValid)
            {
                var sessionInDb = await
                    db.Sessions.SingleOrDefaultAsync(QueryHelper.GetSessionValidationQuery(sessionInfoObject));
                if (sessionInDb != null)
                {
                    var user = sessionInDb.User;               
                    return Ok(user.GetAdBookmark(db).ToList());
                }

                return Unauthorized();
            }

            return BadRequest();
        }



        /// <summary>
        /// This endpoint creates a new bookmark 
        /// </summary>
        /// <returns>200 : Created |
        /// 400 : Bad Request |
        /// 401 : Session info not found
        /// </returns>
        [HttpPost]
        [Route("api/users/advertisements/bookmark/create")]
        public async Task<IHttpActionResult> CreateAdvertisementBookmark(IDPairDto pair)
        {
            if (ModelState.IsValid)
            {
                var sessionInDb = await
                    db.Sessions.SingleOrDefaultAsync(QueryHelper.GetSessionValidationQuery(pair.session));
                if (sessionInDb != null)
                {
                    var user = sessionInDb.User;
                    user.CreateAdBookmark(db, pair.Id);
                    db.SaveChanges();
                    return Ok();
                }

                return Unauthorized();
            }

            return BadRequest();
        }



        /// <summary>
        /// This endpoint returns a list of requests owned by a user specified by given session information.
        /// </summary>
        /// <returns>200 : sent |
        /// 400 : Bad Request |
        /// 401 : Session info not found
        /// </returns>
        [HttpPost]
        [Route("api/users/requests/me")]
        public async Task<IHttpActionResult> GetRequests(SessionInfoObject sessionInfoObject)
        {
            if (ModelState.IsValid)
            {
                var sessionInDb = await
                    db.Sessions.SingleOrDefaultAsync(QueryHelper.GetSessionValidationQuery(sessionInfoObject));
                if (sessionInDb != null)
                {
                    var user = sessionInDb.User;
                    var requests = db.Requests

                        .Where(QueryHelper.GetRequestValidationQuery()).Where(c => c.User.Id == user.Id)
                        .Select(QueryHelper.GetRequestQuery()).ToList();
                    return Ok(requests);

                }

                return Unauthorized();
            }

            return BadRequest();
        }



        [HttpPost]
        [Route("api/users/advertisements/bookmarked")]


        /// <summary>
        /// Returns number of users
        /// </summary>
        /// <returns>Number of users</returns>
        [Route("api/users/count")]
        [HttpGet]
        public int GetCount()
        {
            return db.Users.Count();
        }
    }
}
