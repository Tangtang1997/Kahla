﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Aiursoft.Pylon.Services;
using Aiursoft.Pylon.Services.ToStargateServer;
using Aiursoft.Pylon.Models.Stargate.ChannelViewModels;
using Kahla.Server.Data;
using Kahla.Server.Events;
using Newtonsoft.Json;
using Kahla.Server.Models;
using Newtonsoft.Json.Serialization;

namespace Kahla.Server.Services
{
    public class KahlaPushService
    {
        private readonly KahlaDbContext _dbContext;
        private readonly PushMessageService _stargatePushService;
        private readonly AppsContainer _appsContainer;
        private readonly ChannelService _channelService;
        private readonly ThirdPartyPushService _thirdPartyPushService;

        public KahlaPushService(
            KahlaDbContext dbContext,
            PushMessageService stargatePushService,
            AppsContainer appsContainer,
            ChannelService channelService,
            ThirdPartyPushService thirdPartyPushService)
        {
            _dbContext = dbContext;
            _stargatePushService = stargatePushService;
            _appsContainer = appsContainer;
            _channelService = channelService;
            _thirdPartyPushService = thirdPartyPushService;
        }

        private static string _Serialize(object obj)
        {
            return JsonConvert.SerializeObject(obj, Formatting.Indented, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            });
        }

        public async Task<CreateChannelViewModel> Init(string userId)
        {
            var token = await _appsContainer.AccessToken();
            var channel = await _channelService.CreateChannelAsync(token, $"Kahla User Channel for Id: {userId}");
            return channel;
        }

        public async Task NewMessageEvent(KahlaUser receiver, Conversation conversation, string content, KahlaUser sender, bool alert)
        {
            var token = await _appsContainer.AccessToken();
            var channel = receiver.CurrentChannel;
            var newMessageEvent = new NewMessageEvent
            {
                Type = EventType.NewMessage,
                ConversationId = conversation.Id,
                Sender = sender,
                Content = content,
                AESKey = conversation.AESKey,
                Muted = !alert
            };
            var pushTasks = new List<Task>();
            if (channel != -1)
            {
                pushTasks.Add(_stargatePushService.PushMessageAsync(token, channel, _Serialize(newMessageEvent), true));
            }
            if (alert && receiver.Id != sender.Id)
            {
                pushTasks.Add(_thirdPartyPushService.PushAsync(receiver.Id, sender.Email, _Serialize(newMessageEvent)));
            }
            await Task.WhenAll(pushTasks);
        }

        public async Task NewFriendRequestEvent(string receiverId, string requesterId)
        {
            var token = await _appsContainer.AccessToken();
            var receiver = await _dbContext.Users.FindAsync(receiverId);
            var requester = await _dbContext.Users.FindAsync(requesterId);
            var channel = receiver.CurrentChannel;
            var newFriendRequestEvent = new NewFriendRequestEvent
            {
                Type = EventType.NewFriendRequestEvent,
                RequesterId = requesterId
            };
            if (channel != -1)
                await _stargatePushService.PushMessageAsync(token, channel, _Serialize(newFriendRequestEvent), true);
            await _thirdPartyPushService.PushAsync(receiver.Id, requester.Email, _Serialize(newFriendRequestEvent));
        }

        public async Task WereDeletedEvent(string receiverId)
        {
            var token = await _appsContainer.AccessToken();
            var user = await _dbContext.Users.FindAsync(receiverId);
            var channel = user.CurrentChannel;
            var wereDeletedEvent = new WereDeletedEvent
            {
                Type = EventType.WereDeletedEvent
            };
            if (channel != -1)
                await _stargatePushService.PushMessageAsync(token, channel, _Serialize(wereDeletedEvent), true);
            await _thirdPartyPushService.PushAsync(user.Id, "postermaster@aiursoft.com", _Serialize(wereDeletedEvent));
        }

        public async Task FriendAcceptedEvent(string receiverId)
        {
            var token = await _appsContainer.AccessToken();
            var user = await _dbContext.Users.FindAsync(receiverId);
            var channel = user.CurrentChannel;
            var friendAcceptedEvent = new FriendAcceptedEvent
            {
                Type = EventType.FriendAcceptedEvent
            };
            if (channel != -1)
                await _stargatePushService.PushMessageAsync(token, channel, _Serialize(friendAcceptedEvent), true);
            await _thirdPartyPushService.PushAsync(user.Id, "postermaster@aiursoft.com", _Serialize(friendAcceptedEvent));
        }
    }
}
