﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using TwitchLib.Api;
using TwitchLib.Api.V5.Models.Users;
using TwitchLib.Client;
using TwitchLib.Client.Models;

namespace IO.Core.ChatServices
{
    public class UptimeChatService : BaseChatService, IChatService
    {
        private readonly TwitchAPI twitchAPI;

        private const string upTimeResponse = "{0}, the stream has been up for {1}. {2}";
        private const string offlineResponse = "{0}, the stream is currently offline.  Hit follow to be notified when we go live!";

        private const int _throttleInSeconds = 300;
        private static DateTime? _commandLastRun;

        public UptimeChatService(TwitchClient applicationTwitchClient, TwitchAPI applicationTwitchAPI) :
            base(applicationTwitchClient)
        {
            twitchAPI = applicationTwitchAPI;
        }

        public List<ChatCommand> AvailableCommands() => new List<ChatCommand>()
                                                                {
                                                                    new ChatCommand("!uptime", null, true)
                                                                };

        public async Task<string> ProcessMessageAsync(ChatMessage chatMessage)
        {
            // Regardless of whether this method should or can respond
            // to this message, return if throttled. (Unless it's initiated by
            // the bot or broadcaster)
            if (!chatMessage.IsBroadcaster &&
                !chatMessage.Username.Equals(Constants.TwitchChatBotUsername, StringComparison.InvariantCultureIgnoreCase) &&
                _commandLastRun.HasValue &&
                _commandLastRun.Value.AddSeconds(_throttleInSeconds) >= DateTime.Now)
                return string.Empty;

            string message = chatMessage.Message;

            if (!string.IsNullOrEmpty(message))
            {
                string[] splitMessage = message.Split(null);

                if (splitMessage[0].ToLower().Equals("!uptime"))
                {
                    string responseMessage = string.Empty;
                    try
                    {
                        if (await twitchAPI.V5.Streams.BroadcasterOnlineAsync(Constants.TwitchChannelId))
                        {
                            TimeSpan? uptime = twitchAPI.V5.Streams.GetUptimeAsync(Constants.TwitchChannelId).Result;
                            if (uptime.HasValue)
                            {
                                string upTimeValueMessage = "";
                                if (uptime.Value.Hours > 0)
                                {
                                    upTimeValueMessage = $"{uptime.Value.Hours} hours and ";
                                }
                                upTimeValueMessage += $"{uptime.Value.Minutes} minutes";

                                string followerMessage = "";
                                if (chatMessage.UserId != Constants.TwitchChannelId)
                                {
                                    UserFollow userFollow = await twitchAPI.V5.Users.CheckUserFollowsByChannelAsync(chatMessage.UserId, Constants.TwitchChannelId);

                                    if (userFollow == null)
                                    {
                                        followerMessage = "Give us a follow so you won't miss a minute next time.";
                                    }
                                }

                                responseMessage = string.Format(upTimeResponse, chatMessage.DisplayName, upTimeValueMessage, followerMessage);

                                _commandLastRun = DateTime.Now;
                                SendMessage(responseMessage);
                            }
                        }
                        else
                        {
                            _commandLastRun = DateTime.Now;
                            responseMessage = string.Format(offlineResponse, chatMessage.DisplayName);
                            SendMessage(responseMessage);
                        }
                    }
                    catch (Exception)
                    {

                    }

                    return responseMessage;
                }
            }
            return string.Empty;
        }
    }
}
