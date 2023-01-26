using System.Collections.Concurrent;
using System.Net;
using System.Runtime.CompilerServices;
using RabbitMQ.Stream.Client;

var singleThreadSinchronizationContext = new MainThreadSynchronizationContext();

var lbAddressResolver = new AddressResolver(new IPEndPoint(IPAddress.Parse("192.168.0.19"), 5552));
var config = new StreamSystemConfig
{
    VirtualHost = "/",
    UserName = "iuri",
    Password = "iuri",
    AddressResolver = lbAddressResolver,
    Endpoints = new List<EndPoint> { lbAddressResolver.EndPoint }
};


Console.WriteLine($"Current thread before create connection {Thread.CurrentThread.ManagedThreadId}");
var stream = await StreamSystem.Create(config).ConfigureAwait(false);

Console.WriteLine($"Current thread after create connection and before create consumer {Thread.CurrentThread.ManagedThreadId}");
var consumer = await stream.CreateRawConsumer(new RawConsumerConfig("RabbitMQ-rabbitmq-0"));
Console.WriteLine($"Current thread after create consumer and before close consumer {Thread.CurrentThread.ManagedThreadId}");


SynchronizationContext.SetSynchronizationContext(singleThreadSinchronizationContext);
//This will cause a deadlock and throw "System.AggregateException: One or more errors occurred"
//see: https://github.com/rabbitmq/rabbitmq-stream-dotnet-client/issues/25
await consumer.Close().ConfigureAwait(false);

Console.WriteLine($"Current SynchronizationContext {SynchronizationContext.Current}");
//This doesn't block the current thread because the callback (code after line 33) is running on a different thread, hence, not causing a deadlock
await Task.Delay(1000).ConfigureAwait(false);

Console.WriteLine($"Current SynchronizationContext after ConfigureAwait(false) {SynchronizationContext.Current?.ToString() ?? "null"}");

//We can even call consumer close here since we are not under the app SynchronizationContext anymore because of ConfigureAwait(false).
await consumer.Close().ConfigureAwait(false);


Console.WriteLine($"Current thread after close consumer {Thread.CurrentThread.ManagedThreadId}");




singleThreadSinchronizationContext.RunOnCurrentThread();


Console.WriteLine("after run SetSynchronizationContext in current thread");
class MainThreadSynchronizationContext : SynchronizationContext

{
    private readonly BlockingCollection<KeyValuePair<SendOrPostCallback, object>> m_queue =
        new ();

    public override void Post(SendOrPostCallback d, object state)
    {
        m_queue.Add(new KeyValuePair<SendOrPostCallback, object>(d, state));
    }

    public void RunOnCurrentThread()

    {
        KeyValuePair<SendOrPostCallback, object> workItem;
        while (m_queue.TryTake(out workItem, Timeout.Infinite)) workItem.Key(workItem.Value);
    }

    public override string ToString() => "MainThreadSynchronizationContext";
}