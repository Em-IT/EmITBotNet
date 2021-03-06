﻿using ir.EmIT.EmITBotNet.NFAUtility;
using System.Collections.Generic;
using System.Linq;
using Telegram.Bot;
using Telegram.Bot.Types;
using System;
using System.Threading.Tasks;

namespace ir.EmIT.EmITBotNet
{
    //todo گذاشتن کتابخانه در نیوگت

    public abstract class EmITBotNetBase
    {
        // کلاینت بات تلگرام
        public TelegramBotClient bot;

        // لیست داده های جلسات کاربران
        public List<SessionData> sessionDataList;
        // داده های جلسه کاربر فعلی
        public SessionData currentSessionData;

        // ماشین خودکار غیرقطعی
        public EmITNFA nfa;

        // لیست کاربران مجاز به استفاده از بات
        public List<long> authenticatedUsers;

        // شیئ دیتابیس
        public EmITBotNetContext db;

        public EmITBotNetBase()
        {
            // ست کردن لیست کاربران مجاز
            authenticatedUsers = getAuthenticatedUsers();

            // تعریف شیئ دلخواه برای کار با دیتابیس
            initDatabase();

            // تعریف nfa
            nfa = new EmITNFA();

            // تعریف لیست جلسه ها برای کاربران مختلف
            //userData = new List<MohammadArianUserData>();
            sessionDataList = new List<SessionData>();

            // تعریف قواعد حرکت بین وضعیت ها، براساس عمل دریافتی
            defineNFARules();

            // تعریف قواعد انجام کار، پس از رسیدن به هر وضعیت
            defineNFARulePostFunctions();
        }

        /// <summary>
        /// پردازش پیام تلگرامی دریافتی
        /// </summary>
        /// <param name="m">پیام تلگرامی دریافتی</param>
        public async void HandleMessage(Message m)
        {
            // بررسی وجود جلسه (سشن) برای کاربر جاری
            getConvertedSessionData(m);

            if (m.Text == null)
                return;

            // بررسی کاربرانی که حق دسترسی به بات را دارند
            if (!isAuthenticated(m))
                return;

            m = convertData(m);

            // عمل ورودی
            string action = m.Text;

            // انجام عمل و حرکت به سمت وضعیت بعدی
            await nfa.move(m, currentSessionData);

            // بررسی اینکه اگر در وضعیت جاری، یک عمل لامبدا وجود دارد، یک حرکت جدید با عمل لامبدا (بدون گرفتن ورودی از کاربر) صورت بگیرد
            while (nfa.currentStateHasLambdaAction(currentSessionData) || nfa.currentStateHasCustomAction(currentSessionData))
            {
                Message m2 = m;
                m2.Text = currentSessionData.nextCustomAction;
                currentSessionData.nextCustomAction = "";
                await nfa.move(m2, currentSessionData);
                //actUsingLambdaAction(m);
            }
            
        }

        /// <summary>
        /// تعریف شیئ دلخواه برای کار با دیتابیس
        /// </summary>
        public abstract void initDatabase();

        /// <summary>
        /// تبدیل داده های پیام بات، قبل از پردازش
        /// </summary>
        /// <param name="m">پیام ورودی</param>
        /// <returns>پیام تبدیل شده</returns>
        public abstract Message convertData(Message m);

        /// <summary>
        /// ست کردن بات از بیرون پروژه
        /// </summary>
        /// <param name="bot">بات</param>
        public void setBot(TelegramBotClient bot)
        {
            this.bot = bot;
        }

        /// <summary>
        /// بررسی وجود جلسه (سشن) برای کاربر جاری
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public SessionData checkSessionAndGetCurrentUserData(Message m)
        {
            long currentUserID = m.Chat.Id;
            if (sessionDataList.Where<SessionData>(ud => ud.telegramUserID == currentUserID).Count() == 0)
            {
                // ساخت جلسه (سشن) برای کاربر جاری با تنظیمات اولیه
                addNewUserSession(currentUserID);
            }
            // پیدا کردن سشن مربوط به کاربر جاری
            //currentUserData = (MohammadArianUserData)userData.Where<UserData>(ud => ud.userID == currentUserID).First();
            return sessionDataList.Where<SessionData>(ud => ud.telegramUserID == currentUserID).First();
        }

        /// <summary>
        /// بررسی جلسه فعلی، گرفتن اطلاعات آن جلسه و تبدیل به کلاس خاص این پروژه
        /// </summary>
        /// <param name="m">پیام دریافتی فعلی</param>
        public abstract void getConvertedSessionData(Message m);

        /// <summary>
        /// افزودن جلسه جدید برای کاربر جدید
        /// </summary>
        /// <param name="currentUserID">شناسه کاربر جدید</param>
        public abstract void addNewUserSession(long currentUserID);

        /// <summary>
        /// بررسی کاربرانی که حق دسترسی به بات را دارند
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public bool isAuthenticated(Message m)
        {
            // اگر لیست کاربران مجاز خالی باشد، یعنی همه دسترسی دارند
            if (authenticatedUsers.Count() == 0)
                return true;

            if (!authenticatedUsers.Contains(m.Chat.Id))
            {
                //await bot.SendTextMessageAsync(target, "با عرض معذرت، شما مجوز دسترسی به این بات را ندارید");
                bot.SendTextMessageAsync(m.Chat.Id, "با عرض معذرت، شما مجوز دسترسی به این بات را ندارید");
                return false;
            }
            return true;
        }

        //todo ایجاد سازوکاری که کاربر برنامه نویس بفهمد خالی بودن لیست یعنی همه کاربران
        /// <summary>
        /// گرفتن لیست کاربران مجاز به کار
        /// </summary>
        /// <returns>لیست شناسه کاربران مجاز به کار</returns>
        public abstract List<long> getAuthenticatedUsers();

        /// <summary>
        /// تعیین قواعد بررسی وضعیت فعلی و ورودی دریافتی و تعیین وضعیت بعدی
        /// </summary>
        public abstract void defineNFARules();

        /// <summary>
        /// بررسی وضعیت جدید و انجام کاری که پس از رسیدن به وضعیت جدید باید انجام شود
        /// </summary>
        public abstract void defineNFARulePostFunctions();

        /*/// <summary>
        /// حرکت با ورودی لامبدا (بدون ورودی)
        /// </summary>
        /// <param name="m"></param>
        private void actUsingLambdaAction(Message m)
        {
            actUsingCustomAction(m, "");
        }*/

        public void actUsingCustomAction(Message m, string action)
        {
            /*Message mPrim = m;
            mPrim.Text = action;
            mPrim.Date = DateTime.Now;

            HandleMessage(mPrim);*/

            currentSessionData.nextCustomAction = action;
        }
    }
}
