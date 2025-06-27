using IXICore;
using IXICore.Meta;
using IXICore.Streaming;
using Spixi;
using SPIXI.Interfaces;
using SPIXI.Lang;
using SPIXI.Meta;
using System.Text;

namespace SPIXI.VoIP
{

    public class VoIPManager
    {
        public static byte[]? currentCallSessionId { get; private set; }
        public static Friend currentCallContact { get; private set; }
        public static bool currentCallAccepted { get; private set; }
        public static bool currentCallCalleeAccepted { get; private set; }
        public static string? currentCallCodec { get; private set; }
        public static long currentCallStartedTime { get; private set; }

        static IAudioPlayer? audioPlayer = null;
        static IAudioRecorder? audioRecorder = null;

        static long lastPacketReceivedTime = 0;
        static Thread? lastPacketReceivedCheckThread = null;
        private static readonly object lastPacketReceivedLock = new object();

        static bool currentCallInitiator = false;
        public static long currentCallInitiated { get; private set; } = 0;

        public static bool isInitiated()
        {
            if(currentCallSessionId != null)
            {
                return true;
            }
            return false;
        }

        public static void initiateCall(Friend friend)
        {
            if (currentCallSessionId != null)
            {
                return;
            }

            SSpixiPermissions.requestAudioRecordingPermissions();

            currentCallSessionId = Guid.NewGuid().ToByteArray();
            currentCallContact = friend;
            currentCallCalleeAccepted = false;
            currentCallAccepted = true;
            currentCallCodec = null;
            currentCallInitiator = true;
            currentCallInitiated = Clock.getTimestamp();

            string codecs = String.Join("|", SSpixiCodecInfo.getSupportedAudioCodecs());

            Node.addMessageWithType(currentCallSessionId, FriendMessageType.voiceCall, friend.walletAddress, 0, "", true, null, 0, false);
            StreamProcessor.sendAppRequest(friend, "spixi.voip", currentCallSessionId, Encoding.UTF8.GetBytes(codecs));
            ((SpixiContentPage)Application.Current.MainPage.Navigation.NavigationStack.Last()).displayCallBar(currentCallSessionId, SpixiLocalization._SL("global-call-dialing") + " " + friend.nickname + "...", 0);
            
            aquirePowerLocks();
            SPlatformUtils.startDialtone(DialtoneType.dialing);
        }

        public static bool onReceivedCall(Friend friend, byte[] session_id, byte[] data)
        {
            if (currentCallSessionId != null)
            {
                if (!currentCallSessionId.SequenceEqual(session_id))
                {
                    StreamProcessor.sendAppRequestReject(friend, session_id);
                }
                return false;
            }

            currentCallSessionId = session_id;
            currentCallContact = friend;
            currentCallCalleeAccepted = true;
            currentCallAccepted = false;
            currentCallCodec = null;
            currentCallInitiator = false;
            currentCallInitiated = Clock.getTimestamp();

            string codecs_str = Encoding.UTF8.GetString(data);

            string[] codecs = codecs_str.Split(new string[] { "|" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var codec in codecs)
            {
                if (SSpixiCodecInfo.isCodecSupported(codec))
                {
                    currentCallCodec = codec;
                    break;
                }
            }
            if (currentCallCodec == null)
            {
                Logging.error("Unsupported audio codecs: " + codecs_str);
                rejectCall(session_id);
                return false;
            }
            aquirePowerLocks();
            SPlatformUtils.startRinging();
            return true;
        }

        private static void aquirePowerLocks()
        {
            SPowerManager.AquireLock("screenDim");
            SPowerManager.AquireLock("partial");
            SPowerManager.AquireLock("wifi");
            SPowerManager.AquireLock("proximityScreenOff");
        }

        private static void releasePowerLocks()
        {
            SPowerManager.ReleaseLock("screenDim");
            SPowerManager.ReleaseLock("partial");
            SPowerManager.ReleaseLock("wifi");
            SPowerManager.ReleaseLock("proximityScreenOff");
        }

        private static void startVoIPSession()
        {
            if (currentCallCodec == null)
            {
                Logging.error("No current call codec!");
                return;
            }

            SPlatformUtils.stopDialtone();
            SPlatformUtils.stopRinging();
            try
            {
                audioPlayer = SAudioPlayer.Instance();
                audioPlayer.start(currentCallCodec);

                audioRecorder = SAudioRecorder.Instance();
                audioRecorder.start(currentCallCodec);
                audioRecorder.setOnSoundDataReceived((data) =>
                {
                    StreamProcessor.sendAppData(currentCallContact, currentCallSessionId, data);
                });
                currentCallStartedTime = Clock.getTimestamp();
                startLastPacketReceivedCheck();
            }
            catch(Exception e)
            {
                Logging.error("Exception occured while starting VoIP session: " + e);
                hangupCall(currentCallSessionId);
            }
        }

        private static void endVoIPSession()
        {
            SPlatformUtils.stopRinging();

            try
            {
                if (audioPlayer != null)
                {
                    audioPlayer.Dispose();
                    audioPlayer = null;
                }
            }
            catch (Exception e)
            {
                audioPlayer = null;
                Logging.error("Exception occured in endVoIPSession 1: " + e);
            }

            try
            {
                if (audioRecorder != null)
                {
                    audioRecorder.Dispose();
                    audioRecorder = null;
                }
            }
            catch (Exception e)
            {
                audioRecorder = null;
                Logging.error("Exception occured in endVoIPSession 2: " + e);
            }

            if (currentCallContact != null)
            {
                bool callAccepted = currentCallAccepted && currentCallCalleeAccepted;
                long callDuration = currentCallStartedTime > 0 ? Clock.getTimestamp() - currentCallStartedTime : 0;
                var fm = currentCallContact.endCall(currentCallSessionId, currentCallAccepted && currentCallCalleeAccepted, callDuration, currentCallInitiator);
                if (fm == null)
                {
                    Logging.warn("Cannot end call, no message with session ID exists.");
                } else
                {
                    var tmp_messages = currentCallContact.getMessages(0);
                    if (callAccepted == true && tmp_messages.Last() != fm)
                    {
                        fm.message = callDuration.ToString();
                        Node.addMessageWithType(null, FriendMessageType.voiceCallEnd, currentCallContact.walletAddress, 0, fm.message, currentCallInitiator, null, 0, false);
                    }
                    else
                    {
                        fm.type = FriendMessageType.voiceCallEnd;
                        if (callAccepted)
                        {
                            fm.message = callDuration.ToString();
                        }
                        IxianHandler.localStorage.requestWriteMessages(currentCallContact.walletAddress, 0);
                        UIHelpers.insertMessage(currentCallContact, 0, fm);
                    }
                }

            }

            currentCallSessionId = null;
            currentCallContact = null;
            currentCallCalleeAccepted = false;
            currentCallAccepted = false;
            currentCallCodec = null;
            currentCallStartedTime = 0;
            currentCallInitiated = 0;
            lock (lastPacketReceivedLock)
            {
                lastPacketReceivedTime = 0;
                if (lastPacketReceivedCheckThread != null)
                {
                    try
                    {
                        lastPacketReceivedCheckThread.Interrupt();
                    }
                    catch (Exception)
                    {
                    }
                    lastPacketReceivedCheckThread = null;
                }
            }
            try
            {
                releasePowerLocks();
            }
            catch (Exception e)
            {
                Logging.error("Exception occured in endVoIPSession 3: " + e);
            }
            ((SpixiContentPage)Application.Current.MainPage.Navigation.NavigationStack.Last()).hideCallBar();
        }

        public static void acceptCall(byte[] session_id)
        {
            if (!hasSession(session_id))
            {
                return;
            }

            if (currentCallAccepted)
            {
                return;
            }

            SSpixiPermissions.requestAudioRecordingPermissions();

            currentCallAccepted = true;
            StreamProcessor.sendAppRequestAccept(currentCallContact, session_id, Encoding.UTF8.GetBytes(currentCallCodec));
            startVoIPSession();
            if (currentCallContact != null)
            {
                ((SpixiContentPage)Application.Current.MainPage.Navigation.NavigationStack.Last()).displayCallBar(currentCallSessionId, SpixiLocalization._SL("global-call-in-call") + " - " + currentCallContact.nickname, currentCallStartedTime);
            }
        }

        public static void onAcceptedCall(byte[] session_id, byte[] data)
        {
            if (!hasSession(session_id))
            {
                return;
            }

            if(currentCallCalleeAccepted)
            {
                return;
            }

            currentCallCodec = Encoding.UTF8.GetString(data);
            currentCallCalleeAccepted = true;
            startVoIPSession();
            if (currentCallContact == null)
                return;
            ((SpixiContentPage)Application.Current.MainPage.Navigation.NavigationStack.Last()).displayCallBar(currentCallSessionId, SpixiLocalization._SL("global-call-in-call") + " - " + currentCallContact.nickname, currentCallStartedTime);
        }

        public static void rejectCall(byte[] session_id)
        {
            if (!hasSession(session_id))
            {
                return;
            }
            StreamProcessor.sendAppRequestReject(currentCallContact, session_id);
            endVoIPSession();
        }

        public static void onRejectedCall(byte[] session_id)
        {
            if (!hasSession(session_id))
            {
                return;
            }
            SPlatformUtils.startDialtone(DialtoneType.busy);
            ((SpixiContentPage)Application.Current.MainPage.Navigation.NavigationStack.Last()).hideCallBar();
            endVoIPSession();
        }

        public static void hangupCall(byte[] session_id, bool error = false)
        {
            if (session_id == null)
            {
                session_id = currentCallSessionId;
            }
            if (error)
            {
                SPlatformUtils.startDialtone(DialtoneType.error);
            }
            else
            {
                SPlatformUtils.stopDialtone();
            }
            StreamProcessor.sendAppEndSession(currentCallContact, session_id);
            ((SpixiContentPage)Application.Current.MainPage.Navigation.NavigationStack.Last()).hideCallBar();
            endVoIPSession();
        }

        public static void onHangupCall(byte[] session_id)
        {
            if (!hasSession(session_id))
            {
                return;
            }
            SPlatformUtils.stopDialtone();
            ((SpixiContentPage)Application.Current.MainPage.Navigation.NavigationStack.Last()).hideCallBar();
            endVoIPSession();
        }

        public static void onData(byte[] session_id, byte[] data)
        {
            if (!hasSession(session_id))
            {
                return;
            }
            if (audioPlayer != null)
            {
                audioPlayer.write(data);
                lastPacketReceivedTime = Clock.getTimestamp();
            }
        }

        public static bool hasSession(byte[] session_id)
        {
            if(currentCallSessionId != null && session_id != null && currentCallSessionId.SequenceEqual(session_id))
            {
                return true;
            }
            return false;
        }

        private static void startLastPacketReceivedCheck()
        {
            lock (lastPacketReceivedLock)
            {
                lastPacketReceivedTime = Clock.getTimestamp();
                if (lastPacketReceivedCheckThread != null)
                {
                    try
                    {
                        lastPacketReceivedCheckThread.Interrupt();
                        lastPacketReceivedCheckThread.Join();
                    }
                    catch (Exception)
                    {
                    }
                    lastPacketReceivedCheckThread = null;
                }
                lastPacketReceivedCheckThread = new Thread(lastPacketReceivedCheck);
                lastPacketReceivedCheckThread.IsBackground = true;
                lastPacketReceivedCheckThread.Start();
            }
        }

        private static void lastPacketReceivedCheck()
        {
            try
            {
                while (true)
                {
                    lock (lastPacketReceivedLock)
                    {
                        if (currentCallStartedTime == 0 || lastPacketReceivedTime + 10 <= Clock.getTimestamp())
                        {
                            break;
                        }
                    }
                    Thread.Sleep(1000);
                }
            }
            catch (ThreadInterruptedException)
            {

            }
            finally
            {
                lock (lastPacketReceivedLock)
                {
                    lastPacketReceivedCheckThread = null;
                }
                hangupCall(currentCallSessionId, true);
            }
        }

        public static void setVolume(float volume)
        {
            if(audioPlayer != null)
            {
                audioPlayer.setVolume(volume);
            }
        }
    }
}
