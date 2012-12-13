﻿namespace NServiceBus.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;

    public static class RabbitMqTransportMessageExtensions
    {
        public static IBasicProperties FillRabbitMqProperties(this TransportMessage message, IBasicProperties properties)
        {
            properties.MessageId = Guid.NewGuid().ToString();

            if (!string.IsNullOrEmpty(message.CorrelationId))
                properties.CorrelationId = message.CorrelationId;


            if (message.TimeToBeReceived < TimeSpan.MaxValue)
                properties.Expiration = message.TimeToBeReceived.TotalMilliseconds.ToString();



            properties.SetPersistent(message.Recoverable);

            if (message.Headers != null)
            {
                properties.Headers = message.Headers;

                if (message.Headers.ContainsKey(Headers.EnclosedMessageTypes))
                {
                    properties.Type = message.Headers[Headers.EnclosedMessageTypes];
                }

                if (message.Headers.ContainsKey(Headers.ContentType))
                    properties.ContentType = message.Headers[Headers.ContentType];

                if (message.ReplyToAddress != null && message.ReplyToAddress != Address.Undefined)
                    properties.ReplyTo = message.ReplyToAddress.Queue;

            }
            else
            {
                properties.Headers = new Dictionary<string, string>();
            }

            properties.Headers["NServiceBus.MessageIntent"] = message.MessageIntent.ToString();

            return properties;
        }



        public static TransportMessage ToTransportMessage(this BasicDeliverEventArgs message)
        {

            var properties = message.BasicProperties;
            var result = new TransportMessage
                {
                    Body = message.Body
                };

            if (properties.IsReplyToPresent())
                result.ReplyToAddress = Address.Parse(properties.ReplyTo);

            return result;
        }
    }

}