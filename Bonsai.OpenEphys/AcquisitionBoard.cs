using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading;

using oni;

namespace Bonsai.OpenEphys
{
    public class AcquisitionBoard : Bonsai.Source<IList<oni.Frame>>
    {

        [Range(0,100)]
        public int USBPort { get; set; }

        public int BufferCount { get; set; }

        public override IObservable<IList<Frame>> Generate()
        {
            return Observable.Using(
                () =>
                {
                    var ctx = new oni.Context("ft600", 0);

                    // Set read pre-allocation size
                    ctx.BlockReadSize = 2048;
                    ctx.BlockWriteSize = 2048;

                    return ctx;
                },
                (ctx) => Observable.Create<Frame>(observer =>
                {

                    ctx.Start(true);
                    var running = true;

                    var thread = new Thread(() =>
                    {
                        while (running)
                        {
                            observer.OnNext(ctx.ReadFrame());
                        }
                    });

                    thread.Start();

                    return () =>
                    {
                        running = false;
                        if (thread != Thread.CurrentThread) thread.Join();
                        ctx.Stop();
                    };
                })
                .Buffer(BufferCount)
                .PublishReconnectable()
                .RefCount()
            );
        } // ctx.Dispose() is called.
    }
}
