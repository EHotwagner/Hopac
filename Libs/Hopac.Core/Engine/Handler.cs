// Copyright (C) by Housemarque, Inc.

namespace Hopac.Core {
  using Microsoft.FSharp.Core;
  using System;
  using System.Threading;
  using System.Runtime.CompilerServices;

  /// <summary>Exception handling continuation.</summary>
  public abstract class Handler {
    /// <summary>Do not call this directly unless you know that the handler is not null.</summary>
    internal abstract void DoHandle(ref Worker wr, Exception e);

    /// <summary>Do not call this directly unless you know that the handler is not null.</summary>
    internal abstract Proc GetProc(ref Worker wr);

    [MethodImpl(AggressiveInlining.Flag)]
    internal static void Terminate<X>(ref Worker wr, Cont<X> xK) {
      if (null != xK)
        xK.DoWork(ref wr);
    }

    [MethodImpl(AggressiveInlining.Flag)]
    internal static Proc GetProc<X>(ref Worker wr, ref Cont<X> xKr) {
      var xK = xKr;
      if (null != xK)
        return xK.GetProc(ref wr);
      else
        return AllocProc<X>(ref wr, ref xKr);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static Proc AllocProc<X>(ref Worker wr, ref Cont<X> xKr) {
      Cont<X> xKn = new ProcFinalizer<X>(wr.Scheduler, new Proc());
      var xK = Interlocked.CompareExchange(ref xKr, xKn, null);
      if (null != xK) {
        GC.SuppressFinalize(xKn);
        xKn = xK;
      }
      return xKn.GetProc(ref wr);
    }

    [MethodImpl(AggressiveInlining.Flag)]
    internal static void DoHandle(Handler hr, ref Worker wr, Exception e) {
      if (null != hr)
        hr.DoHandle(ref wr, e);
      else
        DoHandleNull(ref wr, e);
    }

    internal static void PrintExn(String header, Exception e) {
      var first = true;
      do {
        StaticData.writeLine((first ? header : "Caused by: ") + e.ToString());
        first = false;
        e = e.InnerException;
      } while (null != e);
      StaticData.writeLine("No other causes.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void DoHandleNull(ref Worker wr, Exception e) {
      var tlh = wr.Scheduler.TopLevelHandler;
      if (null == tlh) {
        PrintExn("Unhandled exception: ", e);
      } else {
        var uK = new Cont();
        wr.Handler = uK;
        tlh.Invoke(e).DoJob(ref wr, uK);
      }
    }

    private sealed class Cont : Cont<Unit> {
      internal override Proc GetProc(ref Worker wr) {
        throw new NotImplementedException(); // XXX Top level handler has no process.
      }
      internal override void DoHandle(ref Worker wr, Exception e) {
        PrintExn("Top level handler raised: ", e);
      }
      internal override void DoWork(ref Worker wr) { }
      internal override void DoCont(ref Worker wr, Unit value) { }
    }
  }

  internal abstract class Handler_State<T> : Handler {
    internal T State;

    [MethodImpl(AggressiveInlining.Flag)]
    internal Handler_State() { }

    [MethodImpl(AggressiveInlining.Flag)]
    internal Handler_State(T state) { this.State = state; }
  }
}
