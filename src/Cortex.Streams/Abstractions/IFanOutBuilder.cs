using Cortex.Streams.Operators;
using System;

namespace Cortex.Streams.Abstractions
{
    /// <summary>
    /// Builder for configuring multiple sink outputs in a fan-out pattern.
    /// Fan-out allows sending the same data to multiple destinations simultaneously,
    /// which is useful for scenarios like dual-writes, multi-channel notifications,
    /// or parallel processing pipelines.
    /// </summary>
    /// <typeparam name="TIn">The type of the initial input to the stream.</typeparam>
    /// <typeparam name="TCurrent">The current type of data being processed.</typeparam>
    /// <remarks>
    /// <para>
    /// Use <see cref="IFanOutBuilder{TIn, TCurrent}"/> when you need to send data to multiple
    /// sinks without intermediate transformations. For complex branching with transformations,
    /// use <see cref="IStreamBuilder{TIn, TCurrent}.AddBranch"/> instead.
    /// </para>
    /// <example>
    /// <code>
    /// var stream = StreamBuilder&lt;Order&gt;.CreateNewStream("OrderProcessor")
    ///     .Stream()
    ///     .Map(order => EnrichOrder(order))
    ///     .FanOut(fanOut => fanOut
    ///         .To("database", order => SaveToDatabase(order))
    ///         .To("kafka", order => PublishToKafka(order))
    ///         .To("alerts", order => order.Amount > 10000, order => SendAlert(order)))
    ///     .Build();
    /// </code>
    /// </example>
    /// </remarks>
    public interface IFanOutBuilder<TIn, TCurrent>
    {
        /// <summary>
        /// Adds a named sink to the fan-out that receives all data.
        /// </summary>
        /// <param name="name">The unique name of the sink for identification and telemetry.</param>
        /// <param name="sinkFunction">The action to consume data.</param>
        /// <returns>The fan-out builder for method chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="sinkFunction"/> is null.</exception>
        /// <example>
        /// <code>
        /// .FanOut(fanOut => fanOut
        ///     .To("console", x => Console.WriteLine(x))
        ///     .To("file", x => File.AppendAllText("log.txt", x.ToString())))
        /// </code>
        /// </example>
        IFanOutBuilder<TIn, TCurrent> To(string name, Action<TCurrent> sinkFunction);

        /// <summary>
        /// Adds a named sink to the fan-out with a filter predicate.
        /// Only data that matches the predicate will be sent to this sink.
        /// </summary>
        /// <param name="name">The unique name of the sink for identification and telemetry.</param>
        /// <param name="predicate">A filter predicate that determines which data reaches this sink.</param>
        /// <param name="sinkFunction">The action to consume filtered data.</param>
        /// <returns>The fan-out builder for method chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="predicate"/> or <paramref name="sinkFunction"/> is null.</exception>
        /// <example>
        /// <code>
        /// .FanOut(fanOut => fanOut
        ///     .To("all-orders", order => _repository.Save(order))
        ///     .To("high-value", order => order.Amount > 10000, order => _alertService.NotifyHighValue(order))
        ///     .To("priority", order => order.IsPriority, order => _priorityQueue.Enqueue(order)))
        /// </code>
        /// </example>
        IFanOutBuilder<TIn, TCurrent> To(string name, Func<TCurrent, bool> predicate, Action<TCurrent> sinkFunction);

        /// <summary>
        /// Adds a named sink operator to the fan-out.
        /// Use this overload when you need more control over sink lifecycle (Start/Stop).
        /// </summary>
        /// <param name="name">The unique name of the sink for identification and telemetry.</param>
        /// <param name="sinkOperator">The sink operator implementation to consume data.</param>
        /// <returns>The fan-out builder for method chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="sinkOperator"/> is null.</exception>
        /// <example>
        /// <code>
        /// .FanOut(fanOut => fanOut
        ///     .To("kafka", new KafkaSinkOperator&lt;Order&gt;(kafkaConfig))
        ///     .To("elasticsearch", new ElasticsearchSinkOperator&lt;Order&gt;(esConfig)))
        /// </code>
        /// </example>
        IFanOutBuilder<TIn, TCurrent> To(string name, ISinkOperator<TCurrent> sinkOperator);

        /// <summary>
        /// Adds a named sink operator to the fan-out with a filter predicate.
        /// Only data that matches the predicate will be sent to this sink operator.
        /// </summary>
        /// <param name="name">The unique name of the sink for identification and telemetry.</param>
        /// <param name="predicate">A filter predicate that determines which data reaches this sink.</param>
        /// <param name="sinkOperator">The sink operator implementation to consume filtered data.</param>
        /// <returns>The fan-out builder for method chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="predicate"/> or <paramref name="sinkOperator"/> is null.</exception>
        IFanOutBuilder<TIn, TCurrent> To(string name, Func<TCurrent, bool> predicate, ISinkOperator<TCurrent> sinkOperator);

        /// <summary>
        /// Adds a named sink with a transformation before the sink.
        /// Use this when you need to transform data differently for a specific sink.
        /// </summary>
        /// <typeparam name="TOutput">The type of data after transformation.</typeparam>
        /// <param name="name">The unique name of the sink for identification and telemetry.</param>
        /// <param name="mapFunction">A function to transform data before it reaches the sink.</param>
        /// <param name="sinkFunction">The action to consume transformed data.</param>
        /// <returns>The fan-out builder for method chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mapFunction"/> or <paramref name="sinkFunction"/> is null.</exception>
        /// <example>
        /// <code>
        /// .FanOut(fanOut => fanOut
        ///     .To("database", order => _repository.Save(order))
        ///     .ToWithTransform("audit-log", 
        ///         order => new AuditEntry(order.Id, DateTime.UtcNow, "Created"),
        ///         audit => _auditRepository.Log(audit)))
        /// </code>
        /// </example>
        IFanOutBuilder<TIn, TCurrent> ToWithTransform<TOutput>(string name, Func<TCurrent, TOutput> mapFunction, Action<TOutput> sinkFunction);

        /// <summary>
        /// Builds the stream with all configured fan-out sinks.
        /// </summary>
        /// <returns>The built stream instance ready to be started.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no sinks have been configured.</exception>
        IStream<TIn, TCurrent> Build();
    }
}
