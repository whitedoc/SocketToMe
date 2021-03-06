﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Knapcode.SocketToMe.Http;
using Moq;
using Xunit;

namespace Knapcode.SocketToMe.Tests.Http
{
    public class LoggingHandlerTests
    {
        [Fact]
        public async Task LogsSuccessfulExchanges()
        {
            // ARRANGE
            var ts = new TestState();

            // ACT
            var response = await ts.Client.SendAsync(ts.Request);

            // ASSERT
            response.Should().BeSameAs(ts.Response);
            ts.Logger.Verify(x => x.LogAsync(It.Is<ExchangeId>(e => e != ExchangeId.Empty), ts.Request, It.IsAny<CancellationToken>()), Times.Once);
            ts.Logger.Verify(x => x.LogAsync(It.IsAny<ExchangeId>(), It.IsAny<Exception>(), It.IsAny<CancellationToken>()), Times.Never);
            ts.Logger.Verify(x => x.LogAsync(It.Is<ExchangeId>(e => e != ExchangeId.Empty), ts.Response, It.IsAny<CancellationToken>()), Times.Once);
            ts.ExchangeIds.Should().HaveCount(2);
            ts.ExchangeIds[0].Should().Be(ts.ExchangeIds[1]);
        }

        [Fact]
        public void LogsFailedExchanges()
        {
            // ARRANGE
            var ts = new TestState();
            ts.GetResponse = () => { throw ts.Exception; };
            Func<Task> actionAsync = () => ts.Client.SendAsync(ts.Request);

            // ACT, ASSERT
            actionAsync.ShouldThrow<Exception>().Which.Should().BeSameAs(ts.Exception);
            ts.Logger.Verify(x => x.LogAsync(It.Is<ExchangeId>(e => e != ExchangeId.Empty), ts.Request, It.IsAny<CancellationToken>()), Times.Once);
            ts.Logger.Verify(x => x.LogAsync(It.Is<ExchangeId>(e => e != ExchangeId.Empty), ts.Exception, It.IsAny<CancellationToken>()), Times.Once);
            ts.Logger.Verify(x => x.LogAsync(It.IsAny<ExchangeId>(), It.IsAny<HttpResponseMessage>(), It.IsAny<CancellationToken>()), Times.Never);
            ts.ExchangeIds.Should().HaveCount(2);
            ts.ExchangeIds[0].Should().Be(ts.ExchangeIds[1]);
        }

        private class TestState
        {
            public TestState()
            {
                // dependencies
                Logger = new Mock<IHttpMessageLogger>();

                // data
                Request = new HttpRequestMessage(HttpMethod.Get, "http://example/path.html");
                Response = new HttpResponseMessage();
                Exception = new Exception();
                GetResponse = () => Response;
                ExchangeIds = new List<ExchangeId>();

                // setup
                Logger
                    .Setup(x => x.LogAsync(It.IsAny<ExchangeId>(), It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult((object)null))
                    .Callback<ExchangeId, HttpRequestMessage, CancellationToken>((e, m, c) => ExchangeIds.Add(e));
                Logger
                    .Setup(x => x.LogAsync(It.IsAny<ExchangeId>(), It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult((object)null))
                    .Callback<ExchangeId, Exception, CancellationToken>((e, m, c) => ExchangeIds.Add(e));
                Logger
                    .Setup(x => x.LogAsync(It.IsAny<ExchangeId>(), It.IsAny<HttpResponseMessage>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult((object)null))
                    .Callback<ExchangeId, HttpResponseMessage, CancellationToken>((e, m, c) => ExchangeIds.Add(e));

                Target = new LoggingHandler(Logger.Object) { InnerHandler = new TestHandler(this) };
                Client = new HttpClient(Target);
            }

            public List<ExchangeId> ExchangeIds { get; set; }

            public Exception Exception { get; set; }

            public HttpResponseMessage Response { get; set; }

            public HttpRequestMessage Request { get; set; }

            public HttpClient Client { get; set; }

            public LoggingHandler Target { get; set; }

            public Func<HttpResponseMessage> GetResponse { get; set; }

            public Mock<IHttpMessageLogger> Logger { get; set; }
        }

        private class TestHandler : DelegatingHandler
        {
            private readonly TestState _testState;

            public TestHandler(TestState testState)
            {
                _testState = testState;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_testState.GetResponse());
            }
        }
    }
}
