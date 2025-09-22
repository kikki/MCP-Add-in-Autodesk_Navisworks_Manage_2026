using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace waabe_navi_mcp_server.Services.Backends
{
    /// <summary>
    /// Provides a bridge into the Navisworks UI thread without relying on Application.MainWindow.
    /// - Captures the UI SynchronizationContext from the Navisworks thread.
    /// - Allows scheduling synchronous or asynchronous delegates on the UI thread.
    /// - Used by backends and services that must execute UI-bound Navisworks API calls.
    /// </summary>
    public static class UiThread
    {
        private static SynchronizationContext _uiCtx;
        private static int _uiThreadId;

        public static bool IsInitialized => _uiCtx != null;


        /// <summary>
        /// Initializes the UI thread bridge from the current thread.
        /// - Must be called on the Navisworks UI thread (e.g. in AddInPlugin.OnLoaded).
        /// - Captures the SynchronizationContext and thread ID for later use.
        /// </summary>
        public static void InitializeFromCurrentThread()
        {
            var ctx = SynchronizationContext.Current;
            if (ctx == null)
                throw new InvalidOperationException(
                    "UiThread.InitializeFromCurrentThread: Kein SynchronizationContext auf diesem Thread. " +
                    "Unbedingt vom Navisworks-UI-Thread aufrufen (z.B. im AddInPlugin.OnLoaded).");

             
            var typeName = ctx.GetType().Name;
            var isUiCtx =
                typeName.IndexOf("WindowsFormsSynchronizationContext", StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.IndexOf("DispatcherSynchronizationContext", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!isUiCtx)
                throw new InvalidOperationException(
                    $"UiThread.InitializeFromCurrentThread: Unerwarteter Context-Typ '{ctx.GetType().FullName}'. " +
                    "Vom echten NW-UI-Thread initialisieren.");

            _uiCtx = ctx;
            _uiThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>
        /// Ensures that the UI thread bridge has been initialized.
        /// - Throws InvalidOperationException if not initialized.
        /// </summary>
        public static void VerifyInitialized()
        {
            if (_uiCtx == null)
                throw new InvalidOperationException("UiThread not initialized.");
        }

        /// <summary>
        /// Indicates whether the current execution is on the captured UI thread.
        /// - Returns false if not initialized.
        /// </summary>
        public static bool IsOnUiThread =>
            _uiCtx != null && Thread.CurrentThread.ManagedThreadId == _uiThreadId;

        /// <summary>
        /// Invokes an action asynchronously on the UI thread.
        /// - Executes immediately if already on the UI thread.
        /// - Otherwise posts the action to the UI SynchronizationContext.
        /// </summary>
        public static Task InvokeAsync(Action action)
        {
            VerifyInitialized();
            if (IsOnUiThread) { action(); return Task.CompletedTask; }

            var tcs = new TaskCompletionSource<object>();
            _uiCtx.Post(_ =>
            {
                try { action(); tcs.SetResult(null); }
                catch (Exception ex) { tcs.SetException(ex); }
            }, null);
            return tcs.Task;
        }

        /// <summary>
        /// Invokes a function on the UI thread and returns its result.
        /// - Executes immediately if already on the UI thread.
        /// - Otherwise posts the function to the UI SynchronizationContext.
        /// </summary>
        public static Task<T> InvokeAsync<T>(Func<T> func)
        {
            VerifyInitialized();
            if (IsOnUiThread) return Task.FromResult(func());

            var tcs = new TaskCompletionSource<T>();
            _uiCtx.Post(_ =>
            {
                try { var r = func(); tcs.SetResult(r); }
                catch (Exception ex) { tcs.SetException(ex); }
            }, null);
            return tcs.Task;
        }

        /// <summary>
        /// Invokes an asynchronous function (Task-returning) on the UI thread.
        /// - Executes immediately if already on the UI thread.
        /// - Otherwise posts the function and awaits its completion.
        /// </summary>
        public static Task InvokeAsync(Func<Task> func)
        {
            VerifyInitialized();
            if (IsOnUiThread) return func();

            var tcs = new TaskCompletionSource<object>();
            _uiCtx.Post(async _ =>
            {
                try { await func().ConfigureAwait(false); tcs.SetResult(null); }
                catch (Exception ex) { tcs.SetException(ex); }
            }, null);
            return tcs.Task;
        }

        /// <summary>
        /// Invokes an asynchronous function returning a result on the UI thread.
        /// - Executes immediately if already on the UI thread.
        /// - Otherwise posts the function and awaits the result.
        /// </summary>
        public static Task<T> InvokeAsync<T>(Func<Task<T>> func)
        {
            VerifyInitialized();
            if (IsOnUiThread) return func();

            var tcs = new TaskCompletionSource<T>();
            _uiCtx.Post(async _ =>
            {
                try { var r = await func().ConfigureAwait(false); tcs.SetResult(r); }
                catch (Exception ex) { tcs.SetException(ex); }
            }, null);
            return tcs.Task;
        }

        /// <summary>
        /// Runs a synchronous function on the UI thread and returns its result.
        /// - If called on the UI thread, executes directly.
        /// - Otherwise posts it to the UI SynchronizationContext.
        /// </summary> 
        public static Task<T> RunAsync<T>(Func<T> func)
        {
            VerifyInitialized();
            if (IsOnUiThread) return Task.FromResult(func());

            var tcs = new TaskCompletionSource<T>();
            _uiCtx.Post(_ =>
            {
                try { var r = func(); tcs.SetResult(r); }
                catch (Exception ex) { tcs.SetException(ex); }
            }, null);
            return tcs.Task;
        }

        /// <summary>
        /// Runs an asynchronous function on the UI thread and returns its result.
        /// - If called on the UI thread, executes directly.
        /// - Otherwise posts it to the UI SynchronizationContext.
        /// </summary> 
        public static Task<T> RunAsync<T>(Func<Task<T>> func)
        {
            VerifyInitialized();
            if (IsOnUiThread) return func();

            var tcs = new TaskCompletionSource<T>();
            _uiCtx.Post(async _ =>
            {
                try { var r = await func().ConfigureAwait(false); tcs.SetResult(r); }
                catch (Exception ex) { tcs.SetException(ex); }
            }, null);
            return tcs.Task;
        }
    }
}