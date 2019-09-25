using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;

namespace Client.App.Extensions
{
    public static class ObservableExtensions
    {
        public static IObservable<string[]> SplitRepeatedPrefixByNewline(this IObservable<string> input)
        {
            return Observable.Create<string[]>(observer =>
            {
                var last = 0;
                string remainder = null;

                return input
                    .Select(s =>
                    {
                        if (last > s.Length) throw new InvalidOperationException("Output shorter than expected");

                        return s.ToObservable()
                            .SplitStreamByNewlines(last);
                    })
                    .SelectMany(observable => observable)
                    .Subscribe(tuple =>
                    {
                        remainder = tuple.remainder;

                        if (tuple.output.Any())
                        {
                            observer.OnNext(tuple.output);
                            last = tuple.last;
                        }
                    }, observer.OnError, () =>
                    {
                        if (!string.IsNullOrEmpty(remainder)) observer.OnNext(new[] {remainder});
                        observer.OnCompleted();
                    });
            });
        }

        public static IObservable<(string[] output, int last, string remainder)> SplitStreamByNewlines(
            this IObservable<char> input, int skip)
        {
            return Observable.Create<(string[] output, int last, string remainder)>(observer =>
            {
                var last = 0;
                var output = new List<string>();
                var current = new StringBuilder();

                return input
                    .Indexed()
                    .Skip(skip)
                    .Where(tuple => tuple.item != '\r')
                    .Subscribe(c =>
                    {
                        if (c.item == '\n')
                        {
                            output.Add(current.ToString());
                            current.Clear();
                            last = c.index + 1;
                        }
                        else
                        {
                            current.Append(c.item);
                        }
                    }, () =>
                    {
                        observer.OnNext((output.ToArray(), last, current.ToString()));
                        observer.OnCompleted();
                    });
            });
        }

        private static IObservable<(T item, int index)> Indexed<T>(this IObservable<T> input)
        {
            return Observable.Create<(T item, int index)>(observer =>
            {
                var index = 0;

                return input
                    .Subscribe(item =>
                    {
                        observer.OnNext((item, index++));
                    }, observer.OnError, observer.OnCompleted);
            });
        }

        public static (IObservable<TFirst>, IObservable<TSecond>) SplitTuple<TFirst, TSecond>(
            this IObservable<(TFirst, TSecond)> observable)
        {
            var replayObservable = observable.Replay();

            var observable1 = Observable.Create<TFirst>(observer =>
            {
                replayObservable.Connect();
                return replayObservable
                    .Subscribe(tuple =>
                        {
                            observer.OnNext(tuple.Item1);
                        },
                        observer.OnError,
                        observer.OnCompleted);
            });

            var observable2 = Observable.Create<TSecond>(observer =>
            {
                replayObservable.Connect();
                return replayObservable
                    .Subscribe(tuple =>
                        {
                            observer.OnNext(tuple.Item2);
                        },
                        observer.OnError,
                        observer.OnCompleted);
            });

            return (observable1, observable2);
        }
    }
}