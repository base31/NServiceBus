using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Transactions;
using Microsoft.WindowsAzure.StorageClient;
using NServiceBus.Unicast.Queuing;
using NServiceBus.Unicast.Transport;
using NUnit.Framework;
using NBehave.Spec.NUnit;

namespace NServiceBus.Unicast.Queueing.Azure.Tests
{
    [TestFixture]
    public class When_receiving_messages : AzureQueueFixture
    {
        [Test]
        public void Has_messages_should_indicate_if_messages_exists_int_the_queue()
        {
            queue.HasMessage().ShouldBeFalse();
            AddTestMessage();
            queue.HasMessage().ShouldBeTrue();

        }

        [Test]
        public void The_queue_should_poll_for_messages()
        {
            //setup a message insertion in 0.5 seconds
            Action onstart = () =>
            {
                Thread.Sleep(100);
                AddTestMessage();
            };

            onstart.BeginInvoke(null, null);

            queue.Receive(false)
                .ShouldNotBeNull();

        }
        [Test]
        public void Should_throw_if_non_nservicebus_messages_are_received()
        {
            nativeQueue.AddMessage(new CloudQueueMessage("whatever"));

            Assert.Throws<SerializationException>(() => queue.Receive(false));
        }
        [Test]
        public void Should_default_to_non_transactionable_if_no_ambient_transaction_exists()
        {
            AddTestMessage();
            queue.MessageInvisibleTime = 1;

            queue.Receive(true).ShouldNotBeNull();
            Thread.Sleep(1000);
            queue.Receive(true).ShouldBeNull();
        }

        [Test]
        public void Messages_should_not_reapper_in_the_queue_if_transaction_is_committed()
        {
            AddTestMessage();

            queue.MessageInvisibleTime = 1;
            using (var scope = new TransactionScope())
            {
                queue.Receive(true).ShouldNotBeNull();

                scope.Complete();
            }

            Thread.Sleep(1000);

            queue.Receive(false).ShouldBeNull();
        }

        [Test]
        public void The_received_message_should_reappear_in_the_queue_if_transaction_is_not_comitted()
        {
            AddTestMessage();

            queue.MessageInvisibleTime = 2;
            using (new TransactionScope())
            {
                queue.Receive(true).ShouldNotBeNull();

                //rollback
            }

            queue.Receive(false).ShouldBeNull();

            Thread.Sleep(1000);

            queue.Receive(false).ShouldNotBeNull();
        }

        [Test]
        public void Received_messages_should_be_removed_from_the_queue()
        {
            AddTestMessage();

            queue.MessageInvisibleTime = 1;

            queue.Receive(false);

            Thread.Sleep(1000);

            queue.Receive(false).ShouldBeNull();
        }

        [Test]
        public void Send_messages_without_body_should_be_ok()
        {
            AddTestMessage();

            var message = queue.Receive(false);

            message.Body.ShouldBeNull();
        }

        [Test]
        public void All_properties_should_be_preserved()
        {
            var formatter = new BinaryFormatter();

            using (var stream = new MemoryStream())
            {
                var testMessage = new TestMessage {TestProperty = "Test"};
                formatter.Serialize(stream,testMessage);

                var original = new TransportMessage
                                   {
                                       Body = stream.ToArray(),
                                       MessageIntent = MessageIntentEnum.Send,
                                       CorrelationId = "123",
                                       //Id = "11111",
                                       Recoverable = true,
                                       ReturnAddress= "response",
                                       TimeSent = DateTime.Now,
                                       TimeToBeReceived = TimeSpan.FromHours(1)
                                   };
                AddTestMessage(original);

                var result = queue.Receive(false);

                var resultMessage = formatter.Deserialize(new MemoryStream(result.Body)) as TestMessage;
                resultMessage.TestProperty.ShouldEqual("Test");


                result.MessageIntent.ShouldEqual(original.MessageIntent);
                result.CorrelationId.ShouldEqual(original.CorrelationId);
                result.Id.ShouldNotBeNull();
                result.Recoverable.ShouldEqual(original.Recoverable);
                result.ReturnAddress.ShouldEqual(original.ReturnAddress);
                result.TimeSent.ShouldEqual(original.TimeSent);
                result.TimeToBeReceived.ShouldEqual(original.TimeToBeReceived);

            }
        }
    }

    [Serializable]
    public class TestMessage
    {
        public string TestProperty { get; set; }
    }
}