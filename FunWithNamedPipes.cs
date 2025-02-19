using System;
using System.IO.Pipes;
using System.Text;

namespace KLC_Hawk {

    /// <summary>Contains event data for <see cref="NamedPipeMessageReceiveHandler{TMessage}" /> events.</summary>
    /// <typeparam name="TMessage"></typeparam>
    public class NamedPipeListenerMessageReceivedEventArgs<TMessage> : EventArgs
    {
        /// <summary>Initializes an instance of <see cref="NamedPipeListenerMessageReceivedEventArgs{TMessage}" /> with the specified <paramref name="message" />.</summary>
        /// <param name="message">The message passed by the event.</param>
        public NamedPipeListenerMessageReceivedEventArgs(TMessage message)
        {
            this.Message = message;
        }

        /// <summary>Gets the message passed by the event.</summary>
        public TMessage Message { get; private set; }
    }

    /// <summary>Contains event data for <see cref="NamedPipeListenerErrorEventHandler" /> events.</summary>
    public class NamedPipeListenerErrorEventArgs : EventArgs
    {
        /// <summary>Initializes an instance of <see cref="NamedPipeListenerErrorEventArgs" /> with the specified <paramref name="errorType" /> and <paramref name="exception" />.</summary>
        /// <param name="errorType">A <see cref="NamedPipeListenerErrorType" /> describing the part of the listener process where the error was caught.</param>
        /// <param name="ex">The <see cref="Exception" /> that was thrown.</param>
        public NamedPipeListenerErrorEventArgs(NamedPipeListenerErrorType errorType, Exception ex)
        {
            this.ErrorType = errorType;
            this.Exception = ex;
        }

        /// <summary>Gets a <see cref="NamedPipeListenerErrorType" /> describing the part of the listener process where the error was caught.</summary>
        public NamedPipeListenerErrorType ErrorType { get; private set; }

        /// <summary>Gets the <see cref="Exception" /> that was caught.</summary>
        public Exception Exception { get; private set; }
    }

    /// <summary>Represents a method that will handle an event where a message is received via named pipes.</summary>
    /// <typeparam name="TMessage">The type of message that will be received.</typeparam>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The event data passed by the event, which includes the message that was received.</param>
    public delegate void NamedPipeMessageReceivedHandler<TMessage>(Object sender, NamedPipeListenerMessageReceivedEventArgs<TMessage> e);

    /// <summary>Represents a method that will handle an event that is fired when an exception is caught.</summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The event data passed by the event, included the error type and exception that was caught.</param>
    public delegate void NamedPipeMessageErrorHandler(Object sender, NamedPipeListenerErrorEventArgs e);

    /// <summary>Includes different types of errors that describe where in the listening process an exception was caught.</summary>
    public enum NamedPipeListenerErrorType : byte
    {
        /// <summary>Indicates that an exception was caught while calling <see cref="NamedPipeServerStream.BeginWaitForConnection" />.</summary>
        BeginWaitForConnection = 1,

        /// <summary>Indicates that an exception was caught while calling <see cref="NamedPipeServerStream.EndWaitForConnection" />.</summary>
        EndWaitForConnection = 2,

        /// <summary>Indicates that an exception was caught while deserializing a message received from the named pipe.</summary>
        DeserializeMessage = 3,

        /// <summary>Indicates that an exception was caught while closing or disposing a used named pipe.</summary>
        CloseAndDisposePipe = 4,

        /// <summary>Indicates that an exception was caught while invoking the <see cref="NamedPipeListener{TMessage}.MessageReceived"/> event.</summary>
        NotifyMessageReceived = 5
    }

    /// <summary>A helper class for sending and receiving messages using named pipes.</summary>
    /// <typeparam name="TMessage">The type of message that will be sent or received.</typeparam>
    public class NamedPipeListener : IDisposable
    {
        /// <summary>Occurs when a message is received.</summary>
        public event NamedPipeMessageReceivedHandler<string> MessageReceived;

        /// <summary>Occurs when an exception is caught.</summary>
        public event NamedPipeMessageErrorHandler Error;

        static readonly String DEFAULT_PIPENAME = typeof(NamedPipeListener).FullName;

        NamedPipeServerStream pipeServer;
        private bool IsUserUnique;

        /// <summary>Initializes a new instance of <see cref="NamedPipeListener{TMessage}" /> using the specified <paramref name="pipeName" />.</summary>
        /// <param name="pipeName">The name of the named pipe that will be used to listen on.</param>
        public NamedPipeListener(String pipeName, bool userUnique)
        {
            IsUserUnique = userUnique;

            if (IsUserUnique)
                PipeName = pipeName + "-" + System.Security.Principal.WindowsIdentity.GetCurrent().Name.Replace("\\", "-");
            else
                PipeName = pipeName;
        }

        /// <summary>Initializes a new instance of <see cref="NamedPipeListener{TMessage}" /> using the default pipe name.</summary>
        /// <remarks>The default pipe name is the full name of the type of the instance.</remarks>
        public NamedPipeListener()
            : this(DEFAULT_PIPENAME, false) { }

        /// <summary>The name of the named pipe that will be used to listen on.</summary>
        public String PipeName { get; private set; }

        /// <summary>Starts listening on the named pipe specified for the instance.</summary>
        internal void Start()
        {
            if (pipeServer == null)
            {
                if (IsUserUnique)
                {
                    PipeSecurity pipeSecurity = new PipeSecurity();
                    pipeSecurity.AddAccessRule(new PipeAccessRule(System.Security.Principal.WindowsIdentity.GetCurrent().Name, PipeAccessRights.FullControl, System.Security.AccessControl.AccessControlType.Allow));

                    pipeServer = NamedPipeServerStreamAcl.Create(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Message, PipeOptions.Asynchronous, 0, 0, pipeSecurity);
                }
                else
                {
                    pipeServer = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Message, PipeOptions.Asynchronous);
                }
            }

            try { pipeServer.BeginWaitForConnection(new AsyncCallback(PipeConnectionCallback), null); } catch (Exception ex) { this.OnError(NamedPipeListenerErrorType.BeginWaitForConnection, ex); }
        }

        private void PipeConnectionCallback(IAsyncResult result)
        {
            try
            {
                pipeServer.EndWaitForConnection(result);
            }
            catch (Exception ex)
            {
                this.OnError(NamedPipeListenerErrorType.EndWaitForConnection, ex);
                return;
            }

            string message;
            try
            {
                int totalLen = 0;
                StringBuilder sb = new StringBuilder();
                string messageChunk = string.Empty;
                byte[] messageBuffer = new byte[1000];
                do
                {
                    int len = pipeServer.Read(messageBuffer, 0, messageBuffer.Length);
                    totalLen += len;
                    messageChunk = Encoding.UTF8.GetString(messageBuffer);
                    sb.Append(messageChunk);
                    messageBuffer = new byte[messageBuffer.Length];
                }
                while (!pipeServer.IsMessageComplete);
                message = sb.ToString(0, totalLen);
                //message = (string)formatter.Deserialize(pipeServer); //Insecure
            }
            catch (Exception ex)
            {
                this.OnError(NamedPipeListenerErrorType.DeserializeMessage, ex);
                return;
            }

            try
            {
                this.OnMessageReceived(new NamedPipeListenerMessageReceivedEventArgs<string>(message));
            }
            catch (Exception ex)
            {
                this.OnError(NamedPipeListenerErrorType.NotifyMessageReceived, ex);
                return;
            }

            if (this.End())
            {
                this.Start();
            }
        }

        internal Boolean End()
        {
            try
            {
                pipeServer.Close();
                pipeServer.Dispose();
                pipeServer = null;

                return true;
            }
            catch (Exception ex)
            {
                this.OnError(NamedPipeListenerErrorType.CloseAndDisposePipe, ex);
                return false;
            }
        }

        /*
        private void OnMessageReceived(TMessage message) {
            OnMessageReceived(new NamedPipeListenerMessageReceivedEventArgs<TMessage>(message));
        }
        */

        protected virtual void OnMessageReceived(NamedPipeListenerMessageReceivedEventArgs<string> e)
        {
            MessageReceived?.Invoke(this, e);
        }

        private void OnError(NamedPipeListenerErrorType errorType, Exception ex)
        {
            this.OnError(new NamedPipeListenerErrorEventArgs(errorType, ex));
        }

        protected virtual void OnError(NamedPipeListenerErrorEventArgs e)
        {
            Error?.Invoke(this, e);
        }

        void IDisposable.Dispose()
        {
            if (pipeServer != null)
            {
                try { pipeServer.Disconnect(); } catch { }

                try { pipeServer.Close(); } catch { }

                try { pipeServer.Dispose(); } catch { }
            }
        }

        /// <summary>Sends the specified <paramref name="message" /> to the default named pipe for the message.</summary>        
        /// <param name="message">The message to send.</param>
        public static void SendMessage(string message)
        {
            NamedPipeListener.SendMessage(DEFAULT_PIPENAME, false, message);
        }

        /// <summary>Sends the specified <paramref name="message" /> to the specified named pipe.</summary>
        /// <param name="pipeName">The name of the named pipe the message will be sent to.</param>
        /// <param name="message">The message to send.</param>
        public static void SendMessage(string pipeName, bool userUnique, string message)
        {
            if (userUnique)
                pipeName = pipeName + "-" + System.Security.Principal.WindowsIdentity.GetCurrent().Name.Replace("\\", "-");

            using (var pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.None))
            {
                pipeClient.Connect();

                byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                pipeClient.Write(messageBytes, 0, messageBytes.Length);
                //formatter.Serialize(pipeClient, message); //Insecure
                pipeClient.Flush();

                pipeClient.WaitForPipeDrain();
                pipeClient.Close();
            }
        }
    }
}
