using System;
using System.Collections.Generic;
using System.Threading;
using LibCommon;
using LibCommon.Enums;
using LibCommon.Structs;
using LibCommon.Structs.DBModels;
using LibCommon.Structs.GB28181;
using LibCommon.Structs.WebResponse;
using LibGB28181SipServer;
using LibLogger;
using LibZLMediaKitMediaServer;
using LibZLMediaKitMediaServer.Structs.WebHookRequest;
using LibZLMediaKitMediaServer.Structs.WebRequest.ZLMediaKit;

namespace AKStreamWeb.Services
{
    public static class SipServerService
    {
        /// <summary>
        ///  检查livevideo,stopvideo的相关参数
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="channelId"></param>
        /// <param name="rs"></param>
        /// <param name="mediaServer"></param>
        /// <param name="videoChannel"></param>
        /// <param name="sipChannel"></param>
        /// <param name="sipDevice"></param>
        /// <returns></returns>
        private static bool CheckIt(string deviceId, string channelId, out ResponseStruct rs,
            out ServerInstance mediaServer, out VideoChannel videoChannel, out SipChannel sipChannel,
            out SipDevice sipDevice)
        {
            mediaServer = null;
            videoChannel = null;
            sipChannel = null;
            sipDevice = null;
            rs = new ResponseStruct()
            {
                Code = ErrorNumber.None,
                Message = ErrorMessage.ErrorDic![ErrorNumber.None],
            };
            if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(channelId))
            {
                rs = new ResponseStruct()
                {
                    Code = ErrorNumber.Sys_ParamsIsNotRight,
                    Message = ErrorMessage.ErrorDic![ErrorNumber.Sys_ParamsIsNotRight],
                };


                return false;
            }

            sipDevice = LibGB28181SipServer.Common.SipDevices.FindLast(x => x.DeviceId.Equals(deviceId));

            if (sipDevice == null)
            {
                rs = new ResponseStruct()
                {
                    Code = ErrorNumber.Sip_DeviceNotExists,
                    Message = ErrorMessage.ErrorDic![ErrorNumber.Sip_DeviceNotExists],
                };
                return false;
            }

            sipChannel = sipDevice.SipChannels.FindLast(x => x.DeviceId.Equals(channelId));
            if (sipChannel == null)
            {
                rs = new ResponseStruct()
                {
                    Code = ErrorNumber.Sip_ChannelNotExists,
                    Message = ErrorMessage.ErrorDic![ErrorNumber.Sip_ChannelNotExists],
                };
                return false;
            }

            if (videoChannel == null)
            {
                videoChannel = ORMHelper.Db.Select<VideoChannel>().Where(x => x.DeviceId.Equals(deviceId))
                    .Where(x => x.ChannelId.Equals(channelId))
                    .First();
                if (videoChannel == null)
                {
                    rs = new ResponseStruct()
                    {
                        Code = ErrorNumber.Sys_DB_VideoChannelNotExists,
                        Message = ErrorMessage.ErrorDic![ErrorNumber.Sys_DB_VideoChannelNotExists],
                    };
                    return false;
                }
            }

            if (videoChannel.Enabled == false || videoChannel.MediaServerId.Contains("unknown_server"))
            {
                rs = new ResponseStruct()
                {
                    Code = ErrorNumber.Sys_VideoChannelNotActived,
                    Message = ErrorMessage.ErrorDic![ErrorNumber.Sys_VideoChannelNotActived],
                };
                return false;
            }

            string tmpId = videoChannel.MediaServerId;
            mediaServer = Common.MediaServerList.FindLast(x => x.MediaServerId.Equals(tmpId));
            if (mediaServer == null)
            {
                rs = new ResponseStruct()
                {
                    Code = ErrorNumber.MediaServer_InstanceIsNull,
                    Message = ErrorMessage.ErrorDic![ErrorNumber.MediaServer_InstanceIsNull],
                };
                return false;
            }

            if (!mediaServer.IsKeeperRunning)
            {
                rs = new ResponseStruct()
                {
                    Code = ErrorNumber.Sys_AKStreamKeeperNotRunning,
                    Message = ErrorMessage.ErrorDic![ErrorNumber.Sys_AKStreamKeeperNotRunning],
                };
                return false;
            }

            if (!mediaServer.IsMediaServerRunning)
            {
                rs = new ResponseStruct()
                {
                    Code = ErrorNumber.MediaServer_NotRunning,
                    Message = ErrorMessage.ErrorDic![ErrorNumber.MediaServer_NotRunning],
                };
                return false;
            }

            return true;
        }

        /// <summary>
        /// 获取Sip通道的流媒体相关信息
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="channelId"></param>
        /// <param name="rs"></param>
        /// <returns></returns>
        public static MediaServerStreamInfo GetSipChannelMediaServerStreamInfo(string deviceId, string channelId,
            out ResponseStruct rs)
        {
            rs = new ResponseStruct()
            {
                Code = ErrorNumber.None,
                Message = ErrorMessage.ErrorDic![ErrorNumber.None],
            };
            if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(channelId))
            {
                rs = new ResponseStruct()
                {
                    Code = ErrorNumber.Sys_ParamsIsNotRight,
                    Message = ErrorMessage.ErrorDic![ErrorNumber.Sys_ParamsIsNotRight],
                };
                Logger.Warn($"[{Common.LoggerHead}]->获取Sip通道媒体信息失败->{deviceId}-{channelId}->{JsonHelper.ToJson(rs)}");
                return null;
            }

            var sipDevice = LibGB28181SipServer.Common.SipDevices.FindLast(x => x.DeviceId.Equals(deviceId));

            if (sipDevice == null)
            {
                rs = new ResponseStruct()
                {
                    Code = ErrorNumber.Sip_DeviceNotExists,
                    Message = ErrorMessage.ErrorDic![ErrorNumber.Sip_DeviceNotExists],
                };
                Logger.Warn($"[{Common.LoggerHead}]->获取Sip通道媒体信息失败->{deviceId}-{channelId}->{JsonHelper.ToJson(rs)}");

                return null;
            }

            var sipChannel = sipDevice.SipChannels.FindLast(x => x.DeviceId.Equals(channelId));
            if (sipChannel == null)
            {
                rs = new ResponseStruct()
                {
                    Code = ErrorNumber.Sip_ChannelNotExists,
                    Message = ErrorMessage.ErrorDic![ErrorNumber.Sip_ChannelNotExists],
                };
                Logger.Warn($"[{Common.LoggerHead}]->获取Sip通道媒体信息失败->{deviceId}-{channelId}->{JsonHelper.ToJson(rs)}");

                return null;
            }

            if (sipChannel.PushStatus != PushStatus.PUSHON || sipChannel.ChannelMediaServerStreamInfo == null)
            {
                rs = new ResponseStruct()
                {
                    Code = ErrorNumber.Sip_NotOnPushStream,
                    Message = ErrorMessage.ErrorDic![ErrorNumber.Sip_NotOnPushStream],
                };
                Logger.Warn($"[{Common.LoggerHead}]->获取Sip通道媒体信息失败->{deviceId}-{channelId}->{JsonHelper.ToJson(rs)}");

                return null;
            }

            Logger.Info(
                $"[{Common.LoggerHead}]->获取Sip通道媒体信息成功->{deviceId}-{channelId}->{JsonHelper.ToJson(sipChannel.ChannelMediaServerStreamInfo)}");

            return sipChannel.ChannelMediaServerStreamInfo;
        }

        /// <summary>
        /// 停止GB28181设备推流
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="channelId"></param>
        /// <param name="rs"></param>
        /// <returns></returns>
        public static bool StopLiveVideo(string deviceId, string channelId, out ResponseStruct rs)
        {
            ServerInstance mediaServer;
            VideoChannel videoChannel;
            SipDevice sipDevice;
            SipChannel sipChannel;
            rs = new ResponseStruct()
            {
                Code = ErrorNumber.None,
                Message = ErrorMessage.ErrorDic![ErrorNumber.None],
            };
            var ret = CheckIt(deviceId, channelId, out rs, out mediaServer, out videoChannel, out sipChannel,
                out sipDevice);
            if (ret == false || !rs.Code.Equals(ErrorNumber.None))
            {
                Logger.Warn($"[{Common.LoggerHead}]->停止Sip推流失败->{deviceId}-{channelId}->{JsonHelper.ToJson(rs)}");

                return false;
            }

            if (sipChannel.PushStatus != PushStatus.PUSHON)
            {
                Logger.Info($"[{Common.LoggerHead}]->停止Sip推流成功(此Sip通道本身就处于停止推流状态)->{deviceId}-{channelId}");

                return true;
            }

            lock (sipChannel)
            {
                if (sipChannel.ChannelMediaServerStreamInfo != null)
                {
                    ReqZLMediaKitCloseStreams reqZlMediaKitCloseStreams = new ReqZLMediaKitCloseStreams()
                    {
                        App = sipChannel.ChannelMediaServerStreamInfo.App,
                        Force = true,
                        Stream = sipChannel.Stream,
                        Vhost = sipChannel.ChannelMediaServerStreamInfo.Vhost,
                    };
                    mediaServer.WebApiHelper.CloseStreams(reqZlMediaKitCloseStreams, out rs); //关掉流
                    if (videoChannel.DefaultRtpPort == false)
                    {
                        ReqZLMediaKitCloseRtpPort reqZlMediaKitCloseRtpPort = new ReqZLMediaKitCloseRtpPort()
                        {
                            Stream_Id = sipChannel.Stream,
                        };

                        mediaServer.WebApiHelper.CloseRtpPort(reqZlMediaKitCloseRtpPort, out rs); //关掉rtp端口
                        mediaServer.KeeperWebApi.ReleaseRtpPort(
                            (ushort) sipChannel.ChannelMediaServerStreamInfo.RptPort,
                            out rs); //释放rtp端口
                    }
                }



                SipMethodProxy sipMethodProxy = new SipMethodProxy(5000);
                var retDeInvite = sipMethodProxy.DeInvite(sipChannel, out rs); //通知sip设备停止推流
                if (!rs.Code.Equals(ErrorNumber.None))
                {
                    Logger.Warn($"[{Common.LoggerHead}]->停止Sip推流失败->{deviceId}-{channelId}->{JsonHelper.ToJson(rs)}");

                    return false;
                }


                Logger.Info($"[{Common.LoggerHead}]->停止Sip推流成功->{deviceId}-{channelId}->{retDeInvite}");

                return retDeInvite;
            }
        }

        /// <summary>
        /// 请求GB28181直播流
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="channelId"></param>
        /// <param name="rs"></param>
        /// <param name="rtpPort">保持空或者0，将自动申请端口</param>
        /// <returns></returns>
        public static MediaServerStreamInfo LiveVideo(string deviceId, string channelId, out ResponseStruct rs,
            ushort? rtpPort = 0)
        {
            ServerInstance mediaServer;
            VideoChannel videoChannel;
            SipDevice sipDevice;
            SipChannel sipChannel;
            rs = new ResponseStruct()
            {
                Code = ErrorNumber.None,
                Message = ErrorMessage.ErrorDic![ErrorNumber.None],
            };
            var ret = CheckIt(deviceId, channelId, out rs, out mediaServer, out videoChannel, out sipChannel,
                out sipDevice);
            if (ret == false || !rs.Code.Equals(ErrorNumber.None))
            {
                Logger.Warn($"[{Common.LoggerHead}]->请求Sip推流失败->{deviceId}-{channelId}->{JsonHelper.ToJson(rs)}");

                return null;
            }

            lock (Common.VideoChannelMediaInfosLock)
            {
                var onlineObj = Common.VideoChannelMediaInfos.FindLast(x => x.MainId.Equals(videoChannel.MainId));
                if (onlineObj != null)
                {
                    Logger.Info($"[{Common.LoggerHead}]->请求Sip推流成功(此Sip通道本身就处于推流状态)->{deviceId}-{channelId}");

                    return onlineObj.MediaServerStreamInfo;
                }
            }


            if (sipChannel.PushStatus == PushStatus.PUSHON && sipChannel.ChannelMediaServerStreamInfo != null)
            {
                lock (Common.VideoChannelMediaInfosLock)
                {
                    var videoChannelMediaInfo = new VideoChannelMediaInfo();
                    videoChannelMediaInfo.App = videoChannel.App;
                    videoChannelMediaInfo.Enabled = videoChannel.Enabled;
                    videoChannelMediaInfo.Id = videoChannel.Id;
                    videoChannelMediaInfo.Vhost = videoChannel.Vhost;
                    videoChannelMediaInfo.AutoRecord = videoChannel.AutoRecord;
                    videoChannelMediaInfo.AutoVideo = videoChannel.AutoVideo;
                    videoChannelMediaInfo.ChannelId = videoChannel.ChannelId;
                    videoChannelMediaInfo.ChannelName = videoChannel.ChannelName;
                    videoChannelMediaInfo.CreateTime = videoChannel.CreateTime;
                    videoChannelMediaInfo.DepartmentId = videoChannel.DepartmentId;
                    videoChannelMediaInfo.DepartmentName = videoChannel.DepartmentName;
                    videoChannelMediaInfo.DeviceId = videoChannel.DeviceId;
                    videoChannelMediaInfo.HasPtz = videoChannel.HasPtz;
                    videoChannelMediaInfo.MainId = videoChannel.MainId;
                    videoChannelMediaInfo.UpdateTime = videoChannel.UpdateTime;
                    videoChannelMediaInfo.DefaultRtpPort = videoChannel.DefaultRtpPort;
                    videoChannelMediaInfo.DeviceNetworkType = videoChannel.DeviceNetworkType;
                    videoChannelMediaInfo.DeviceStreamType = videoChannel.DeviceStreamType;
                    videoChannelMediaInfo.IpV4Address = videoChannel.IpV4Address;
                    videoChannelMediaInfo.IpV6Address = videoChannel.IpV6Address;
                    videoChannelMediaInfo.MediaServerId = videoChannel.MediaServerId;
                    videoChannelMediaInfo.NoPlayerBreak = videoChannel.NoPlayerBreak;
                    videoChannelMediaInfo.PDepartmentId = videoChannel.PDepartmentId;
                    videoChannelMediaInfo.PDepartmentName = videoChannel.PDepartmentName;
                    videoChannelMediaInfo.RtpWithTcp = videoChannel.RtpWithTcp;
                    videoChannelMediaInfo.VideoDeviceType = videoChannel.VideoDeviceType;
                    videoChannelMediaInfo.VideoSrcUrl = videoChannel.VideoSrcUrl;
                    videoChannelMediaInfo.MethodByGetStream = videoChannel.MethodByGetStream;

                    videoChannelMediaInfo.MediaServerStreamInfo = new MediaServerStreamInfo();
                    videoChannelMediaInfo.MediaServerStreamInfo.App = sipChannel.ChannelMediaServerStreamInfo.App;
                    videoChannelMediaInfo.MediaServerStreamInfo.Ssrc = sipChannel.ChannelMediaServerStreamInfo.Ssrc;
                    videoChannelMediaInfo.MediaServerStreamInfo.Stream = sipChannel.ChannelMediaServerStreamInfo.Stream;
                    videoChannelMediaInfo.MediaServerStreamInfo.Vhost = sipChannel.ChannelMediaServerStreamInfo.Vhost;
                    videoChannelMediaInfo.MediaServerStreamInfo.PlayerList = new List<MediaServerStreamPlayerInfo>();
                    videoChannelMediaInfo.MediaServerStreamInfo.StartTime =
                        sipChannel.ChannelMediaServerStreamInfo.StartTime;
                    videoChannelMediaInfo.MediaServerStreamInfo.RptPort =
                        sipChannel.ChannelMediaServerStreamInfo.RptPort;
                    videoChannelMediaInfo.MediaServerStreamInfo.StreamPort =
                        sipChannel.ChannelMediaServerStreamInfo.StreamPort;
                    videoChannelMediaInfo.MediaServerStreamInfo.MediaServerId =
                        sipChannel.ChannelMediaServerStreamInfo.MediaServerId;
                    videoChannelMediaInfo.MediaServerStreamInfo.MediaServerIp =
                        sipChannel.ChannelMediaServerStreamInfo.MediaServerId;
                    videoChannelMediaInfo.MediaServerStreamInfo.PushSocketType =
                        sipChannel.ChannelMediaServerStreamInfo.PushSocketType;
                    videoChannelMediaInfo.MediaServerStreamInfo.StreamIp =
                        sipChannel.ChannelMediaServerStreamInfo.StreamIp;
                    videoChannelMediaInfo.MediaServerStreamInfo.StreamTcpId =
                        sipChannel.ChannelMediaServerStreamInfo.StreamTcpId;
                    videoChannelMediaInfo.MediaServerStreamInfo.Params = sipChannel.ChannelMediaServerStreamInfo.Params;
                    videoChannelMediaInfo.MediaServerStreamInfo.PlayUrl = new List<string>();

                    foreach (var url in sipChannel.ChannelMediaServerStreamInfo.PlayUrl)
                    {
                        videoChannelMediaInfo.MediaServerStreamInfo.PlayUrl.Add(url);
                    }

                    Common.VideoChannelMediaInfos.Add(videoChannelMediaInfo);
                }

                Logger.Info($"[{Common.LoggerHead}]->请求Sip推流成功(此Sip通道本身就处于推流状态)->{deviceId}-{channelId}");

                return sipChannel.ChannelMediaServerStreamInfo;
            }

            ResMediaServerOpenRtpPort openRtpPort;
            if (rtpPort == null || rtpPort == 0) //如果没指定rtp端口，就申请一个
            {
                if (videoChannel.DefaultRtpPort == false) //非使用固定端口，则申请
                {
                    try
                    {
                        openRtpPort =
                            MediaServerService.MediaServerOpenRtpPort(mediaServer.MediaServerId, videoChannel.MainId,
                                out rs);
                        if (openRtpPort == null || !rs.Code.Equals(ErrorNumber.None))
                        {
                            Logger.Warn(
                                $"[{Common.LoggerHead}]->请求Sip推流失败->{deviceId}-{channelId}->{JsonHelper.ToJson(rs)}");

                            return null;
                        }
                    }
                    catch (Exception ex)
                    {
                        rs = new ResponseStruct()
                        {
                            Code = ErrorNumber.MediaServer_OpenRtpPortExcept,
                            Message = ErrorMessage.ErrorDic![ErrorNumber.MediaServer_OpenRtpPortExcept],
                            ExceptMessage = ex.Message,
                            ExceptStackTrace = ex.StackTrace
                        };
                        Logger.Warn(
                            $"[{Common.LoggerHead}]->请求Sip推流失败->{deviceId}-{channelId}->{JsonHelper.ToJson(rs)}");

                        return null;
                    }
                }
                else
                {
                    //使用固定端口就读Config中的固定端口
                    if (mediaServer.Config != null && mediaServer.Config.Data[0] != null)
                    {
                        openRtpPort = new ResMediaServerOpenRtpPort()
                        {
                            Port = (ushort) mediaServer.Config.Data[0].Rtp_Proxy_Port,
                            Stream = videoChannel.MainId,
                        };
                    }
                    else //如果Config为空，则默认使用10000
                    {
                        openRtpPort = new ResMediaServerOpenRtpPort()
                        {
                            Port = 10000,
                            Stream = videoChannel.MainId,
                        };
                    }
                }
            }
            else
            {
                openRtpPort = new ResMediaServerOpenRtpPort()
                {
                    Port = (ushort) rtpPort,
                    Stream = videoChannel.MainId,
                };
            }

            if (!openRtpPort.Stream.Trim().Equals(sipChannel.Stream))
            {
                rs = new ResponseStruct()
                {
                    Code = ErrorNumber.Sip_VideoLiveExcept,
                    Message = ErrorMessage.ErrorDic![ErrorNumber.Sip_VideoLiveExcept] +
                              ",SipChannel.Stream!=OpenRtpPort.Stream",
                };
                Logger.Warn($"[{Common.LoggerHead}]->请求Sip推流失败->{deviceId}-{channelId}->{JsonHelper.ToJson(rs)}");

                return null;
            }

            PushMediaInfo pushMediaInfo = new PushMediaInfo();
            pushMediaInfo.StreamPort = openRtpPort.Port;
            pushMediaInfo.MediaServerIpAddress = mediaServer.IpV4Address;
            pushMediaInfo.PushStreamSocketType =
                videoChannel.RtpWithTcp == true ? PushStreamSocketType.TCP : PushStreamSocketType.UDP;
            SipMethodProxy sipMethodProxy = new SipMethodProxy(5000);
            var liveVideoRet = sipMethodProxy.Invite(sipChannel, pushMediaInfo, out rs);
            if (!rs.Code.Equals(ErrorNumber.None) || liveVideoRet == false)
            {
                Logger.Warn($"[{Common.LoggerHead}]->请求Sip推流失败->{deviceId}-{channelId}->{JsonHelper.ToJson(rs)}");

                return null;
            }

            var taskWait = new WebHookNeedReturnTask(Common.WebHookNeedReturnTask);
            AutoResetEvent myWait = new AutoResetEvent(false);
            taskWait.AutoResetEvent = myWait;

            Common.WebHookNeedReturnTask.TryAdd($"WAITONPUBLISH_{videoChannel.MainId}",
                taskWait);

            var isTimeout = myWait.WaitOne(5000);
            if (!isTimeout)
            {
                rs = new ResponseStruct()
                {
                    Code = ErrorNumber.MediaServer_WaitWebHookTimeOut,
                    Message = ErrorMessage.ErrorDic![ErrorNumber.MediaServer_WaitWebHookTimeOut]
                };
                Logger.Warn($"[{Common.LoggerHead}]->请求Sip推流失败->{deviceId}-{channelId}->{JsonHelper.ToJson(rs)}");

                return null;
            }

            lock (sipChannel)
            {
                ReqForWebHookOnPublish onPublishWebhook = (ReqForWebHookOnPublish) taskWait.OtherObj;
                sipChannel.ChannelMediaServerStreamInfo = new MediaServerStreamInfo();
                sipChannel.ChannelMediaServerStreamInfo.App = onPublishWebhook.App;
                sipChannel.ChannelMediaServerStreamInfo.Ssrc = uint.Parse(sipChannel.SsrcId);
                sipChannel.ChannelMediaServerStreamInfo.Stream = onPublishWebhook.Stream;
                sipChannel.ChannelMediaServerStreamInfo.Vhost = onPublishWebhook.Vhost;
                sipChannel.ChannelMediaServerStreamInfo.PlayerList = new List<MediaServerStreamPlayerInfo>();
                sipChannel.ChannelMediaServerStreamInfo.StartTime = DateTime.Now;
                sipChannel.ChannelMediaServerStreamInfo.RptPort = openRtpPort.Port;
                sipChannel.ChannelMediaServerStreamInfo.StreamPort = (ushort) onPublishWebhook.Port;
                sipChannel.ChannelMediaServerStreamInfo.MediaServerId = onPublishWebhook.MediaServerId;
                sipChannel.ChannelMediaServerStreamInfo.MediaServerIp = mediaServer.IpV4Address;
                sipChannel.ChannelMediaServerStreamInfo.PushSocketType = pushMediaInfo.PushStreamSocketType;
                sipChannel.ChannelMediaServerStreamInfo.StreamIp = onPublishWebhook.Ip;
                sipChannel.ChannelMediaServerStreamInfo.StreamTcpId =
                    videoChannel.RtpWithTcp == true ? onPublishWebhook.Id : null;
                sipChannel.ChannelMediaServerStreamInfo.Params = onPublishWebhook.Params;
                sipChannel.ChannelMediaServerStreamInfo.PlayUrl = new List<string>();
                string exInfo =
                    (!string.IsNullOrEmpty(onPublishWebhook.Vhost) &&
                     !onPublishWebhook.Vhost.Trim().ToLower().Equals("__defaultvhost__"))
                        ? $"?vhost={onPublishWebhook.Vhost}"
                        : "";
                if (mediaServer.UseSsl)
                {
                    sipChannel.ChannelMediaServerStreamInfo.PlayUrl.Add(
                        $"wss://{mediaServer.IpV4Address}:{mediaServer.HttpsPort}/{onPublishWebhook.App}/{onPublishWebhook.Stream}.flv{exInfo}");
                    sipChannel.ChannelMediaServerStreamInfo.PlayUrl.Add(
                        $"https://{mediaServer.IpV4Address}:{mediaServer.HttpsPort}/{onPublishWebhook.App}/{onPublishWebhook.Stream}.flv{exInfo}");
                    sipChannel.ChannelMediaServerStreamInfo.PlayUrl.Add(
                        $"rtsps://{mediaServer.IpV4Address}:{mediaServer.RtspsPort}/{onPublishWebhook.App}/{onPublishWebhook.Stream}{exInfo}");
                    sipChannel.ChannelMediaServerStreamInfo.PlayUrl.Add(
                        $"rtmps://{mediaServer.IpV4Address}:{mediaServer.RtmpsPort}/{onPublishWebhook.App}/{onPublishWebhook.Stream}{exInfo}");
                    sipChannel.ChannelMediaServerStreamInfo.PlayUrl.Add(
                        $"https://{mediaServer.IpV4Address}:{mediaServer.HttpsPort}/{onPublishWebhook.App}/{onPublishWebhook.Stream}/hls.m3u8{exInfo}");
                    sipChannel.ChannelMediaServerStreamInfo.PlayUrl.Add(
                        $"https://{mediaServer.IpV4Address}:{mediaServer.HttpsPort}/{onPublishWebhook.App}/{onPublishWebhook.Stream}.live.ts{exInfo}");
                    sipChannel.ChannelMediaServerStreamInfo.PlayUrl.Add(
                        $"wss://{mediaServer.IpV4Address}:{mediaServer.HttpsPort}/{onPublishWebhook.App}/{onPublishWebhook.Stream}.live.ts{exInfo}");
                    sipChannel.ChannelMediaServerStreamInfo.PlayUrl.Add(
                        $"https://{mediaServer.IpV4Address}:{mediaServer.HttpsPort}/{onPublishWebhook.App}/{onPublishWebhook.Stream}.live.mp4{exInfo}");
                    sipChannel.ChannelMediaServerStreamInfo.PlayUrl.Add(
                        $"wss://{mediaServer.IpV4Address}:{mediaServer.HttpsPort}/{onPublishWebhook.App}/{onPublishWebhook.Stream}.live.mp4{exInfo}");
                }

                sipChannel.ChannelMediaServerStreamInfo.PlayUrl.Add(
                    $"ws://{mediaServer.IpV4Address}:{mediaServer.HttpPort}/{onPublishWebhook.App}/{onPublishWebhook.Stream}.flv{exInfo}");
                sipChannel.ChannelMediaServerStreamInfo.PlayUrl.Add(
                    $"http://{mediaServer.IpV4Address}:{mediaServer.HttpPort}/{onPublishWebhook.App}/{onPublishWebhook.Stream}.flv{exInfo}");
                sipChannel.ChannelMediaServerStreamInfo.PlayUrl.Add(
                    $"rtsp://{mediaServer.IpV4Address}:{mediaServer.RtspPort}/{onPublishWebhook.App}/{onPublishWebhook.Stream}{exInfo}");
                sipChannel.ChannelMediaServerStreamInfo.PlayUrl.Add(
                    $"rtmp://{mediaServer.IpV4Address}:{mediaServer.RtmpPort}/{onPublishWebhook.App}/{onPublishWebhook.Stream}{exInfo}");
                sipChannel.ChannelMediaServerStreamInfo.PlayUrl.Add(
                    $"http://{mediaServer.IpV4Address}:{mediaServer.HttpPort}/{onPublishWebhook.App}/{onPublishWebhook.Stream}/hls.m3u8{exInfo}");
                sipChannel.ChannelMediaServerStreamInfo.PlayUrl.Add(
                    $"http://{mediaServer.IpV4Address}:{mediaServer.HttpPort}/{onPublishWebhook.App}/{onPublishWebhook.Stream}.live.ts{exInfo}");
                sipChannel.ChannelMediaServerStreamInfo.PlayUrl.Add(
                    $"ws://{mediaServer.IpV4Address}:{mediaServer.HttpPort}/{onPublishWebhook.App}/{onPublishWebhook.Stream}.live.ts{exInfo}");
                sipChannel.ChannelMediaServerStreamInfo.PlayUrl.Add(
                    $"http://{mediaServer.IpV4Address}:{mediaServer.HttpPort}/{onPublishWebhook.App}/{onPublishWebhook.Stream}.live.mp4{exInfo}");
                sipChannel.ChannelMediaServerStreamInfo.PlayUrl.Add(
                    $"ws://{mediaServer.IpV4Address}:{mediaServer.HttpPort}/{onPublishWebhook.App}/{onPublishWebhook.Stream}.live.mp4{exInfo}");


                Common.WebHookNeedReturnTask.TryRemove($"WAITONPUBLISH_{videoChannel.MainId}",
                    out WebHookNeedReturnTask task);
                if (task != null)
                {
                    task.Dispose();
                }

                var videoChannelMediaInfo = new VideoChannelMediaInfo();
                videoChannelMediaInfo.App = videoChannel.App;
                videoChannelMediaInfo.Enabled = videoChannel.Enabled;
                videoChannelMediaInfo.Id = videoChannel.Id;
                videoChannelMediaInfo.Vhost = videoChannel.Vhost;
                videoChannelMediaInfo.AutoRecord = videoChannel.AutoRecord;
                videoChannelMediaInfo.AutoVideo = videoChannel.AutoVideo;
                videoChannelMediaInfo.ChannelId = videoChannel.ChannelId;
                videoChannelMediaInfo.ChannelName = videoChannel.ChannelName;
                videoChannelMediaInfo.CreateTime = videoChannel.CreateTime;
                videoChannelMediaInfo.DepartmentId = videoChannel.DepartmentId;
                videoChannelMediaInfo.DepartmentName = videoChannel.DepartmentName;
                videoChannelMediaInfo.DeviceId = videoChannel.DeviceId;
                videoChannelMediaInfo.HasPtz = videoChannel.HasPtz;
                videoChannelMediaInfo.MainId = videoChannel.MainId;
                videoChannelMediaInfo.UpdateTime = videoChannel.UpdateTime;
                videoChannelMediaInfo.DefaultRtpPort = videoChannel.DefaultRtpPort;
                videoChannelMediaInfo.DeviceNetworkType = videoChannel.DeviceNetworkType;
                videoChannelMediaInfo.DeviceStreamType = videoChannel.DeviceStreamType;
                videoChannelMediaInfo.IpV4Address = videoChannel.IpV4Address;
                videoChannelMediaInfo.IpV6Address = videoChannel.IpV6Address;
                videoChannelMediaInfo.MediaServerId = videoChannel.MediaServerId;
                videoChannelMediaInfo.NoPlayerBreak = videoChannel.NoPlayerBreak;
                videoChannelMediaInfo.PDepartmentId = videoChannel.PDepartmentId;
                videoChannelMediaInfo.PDepartmentName = videoChannel.PDepartmentName;
                videoChannelMediaInfo.RtpWithTcp = videoChannel.RtpWithTcp;
                videoChannelMediaInfo.VideoDeviceType = videoChannel.VideoDeviceType;
                videoChannelMediaInfo.VideoSrcUrl = videoChannel.VideoSrcUrl;
                videoChannelMediaInfo.MethodByGetStream = videoChannel.MethodByGetStream;

                videoChannelMediaInfo.MediaServerStreamInfo = new MediaServerStreamInfo();
                videoChannelMediaInfo.MediaServerStreamInfo.App = sipChannel.ChannelMediaServerStreamInfo.App;
                videoChannelMediaInfo.MediaServerStreamInfo.Ssrc = sipChannel.ChannelMediaServerStreamInfo.Ssrc;
                videoChannelMediaInfo.MediaServerStreamInfo.Stream = sipChannel.ChannelMediaServerStreamInfo.Stream;
                videoChannelMediaInfo.MediaServerStreamInfo.Vhost = sipChannel.ChannelMediaServerStreamInfo.Vhost;
                videoChannelMediaInfo.MediaServerStreamInfo.PlayerList = new List<MediaServerStreamPlayerInfo>();
                videoChannelMediaInfo.MediaServerStreamInfo.StartTime =
                    sipChannel.ChannelMediaServerStreamInfo.StartTime;
                videoChannelMediaInfo.MediaServerStreamInfo.RptPort = sipChannel.ChannelMediaServerStreamInfo.RptPort;
                videoChannelMediaInfo.MediaServerStreamInfo.StreamPort =
                    sipChannel.ChannelMediaServerStreamInfo.StreamPort;
                videoChannelMediaInfo.MediaServerStreamInfo.MediaServerId =
                    sipChannel.ChannelMediaServerStreamInfo.MediaServerId;
                videoChannelMediaInfo.MediaServerStreamInfo.MediaServerIp =
                    sipChannel.ChannelMediaServerStreamInfo.MediaServerId;
                videoChannelMediaInfo.MediaServerStreamInfo.PushSocketType =
                    sipChannel.ChannelMediaServerStreamInfo.PushSocketType;
                videoChannelMediaInfo.MediaServerStreamInfo.StreamIp = sipChannel.ChannelMediaServerStreamInfo.StreamIp;
                videoChannelMediaInfo.MediaServerStreamInfo.StreamTcpId =
                    sipChannel.ChannelMediaServerStreamInfo.StreamTcpId;
                videoChannelMediaInfo.MediaServerStreamInfo.Params = sipChannel.ChannelMediaServerStreamInfo.Params;
                videoChannelMediaInfo.MediaServerStreamInfo.PlayUrl = new List<string>();

                foreach (var url in sipChannel.ChannelMediaServerStreamInfo.PlayUrl)
                {
                    videoChannelMediaInfo.MediaServerStreamInfo.PlayUrl.Add(url);
                }

                lock (Common.VideoChannelMediaInfosLock)
                {
                    var obj = Common.VideoChannelMediaInfos.FindLast(x => x.MainId.Equals(videoChannel.MainId));
                    if (obj != null)
                    {
                        Common.VideoChannelMediaInfos.Remove(obj);
                    }

                    Common.VideoChannelMediaInfos.Add(videoChannelMediaInfo);
                }

                Logger.Info(
                    $"[{Common.LoggerHead}]->停止Sip推流成功->{deviceId}-{channelId}->{JsonHelper.ToJson(sipChannel.ChannelMediaServerStreamInfo)}");

                return sipChannel.ChannelMediaServerStreamInfo;
            }
        }

        /// <summary>
        /// 获取通道是否正在推流
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="channelId"></param>
        /// <param name="rs"></param>
        /// <returns></returns>
        public static bool IsLiveVideo(string deviceId, string channelId, out ResponseStruct rs)
        {
            rs = new ResponseStruct()
            {
                Code = ErrorNumber.None,
                Message = ErrorMessage.ErrorDic![ErrorNumber.None],
            };
            if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(channelId))
            {
                rs = new ResponseStruct()
                {
                    Code = ErrorNumber.Sys_ParamsIsNotRight,
                    Message = ErrorMessage.ErrorDic![ErrorNumber.Sys_ParamsIsNotRight],
                };
                Logger.Warn($"[{Common.LoggerHead}]->检查Sip推流状态失败->{deviceId}-{channelId}->{JsonHelper.ToJson(rs)}");

                return false;
            }

            var tmpSipDevice = LibGB28181SipServer.Common.SipDevices.FindLast(x => x.DeviceId.Equals(deviceId));

            if (tmpSipDevice == null)
            {
                rs = new ResponseStruct()
                {
                    Code = ErrorNumber.Sip_DeviceNotExists,
                    Message = ErrorMessage.ErrorDic![ErrorNumber.Sip_DeviceNotExists],
                };
                Logger.Warn($"[{Common.LoggerHead}]->检查Sip推流状态失败->{deviceId}-{channelId}->{JsonHelper.ToJson(rs)}");

                return false;
            }

            var tmpSipChannel = tmpSipDevice.SipChannels.FindLast(x => x.DeviceId.Equals(channelId));
            if (tmpSipChannel == null)
            {
                rs = new ResponseStruct()
                {
                    Code = ErrorNumber.Sip_ChannelNotExists,
                    Message = ErrorMessage.ErrorDic![ErrorNumber.Sip_ChannelNotExists],
                };
                Logger.Warn($"[{Common.LoggerHead}]->检查Sip推流状态失败->{deviceId}-{channelId}->{JsonHelper.ToJson(rs)}");

                return false;
            }

            Logger.Info(
                $"[{Common.LoggerHead}]->检查Sip推流状态成功->{deviceId}-{channelId}->{JsonHelper.ToJson(tmpSipChannel.PushStatus)}");

            if (tmpSipChannel.PushStatus == PushStatus.PUSHON)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 根据ID获取Sip通道
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="channelId"></param>
        /// <param name="rs"></param>
        /// <returns></returns>
        public static SipChannel GetSipChannelById(string deviceId, string channelId, out ResponseStruct rs)
        {
            rs = new ResponseStruct()
            {
                Code = ErrorNumber.None,
                Message = ErrorMessage.ErrorDic![ErrorNumber.None],
            };
            if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(channelId))
            {
                rs = new ResponseStruct()
                {
                    Code = ErrorNumber.Sys_ParamsIsNotRight,
                    Message = ErrorMessage.ErrorDic![ErrorNumber.Sys_ParamsIsNotRight],
                };
                Logger.Warn($"[{Common.LoggerHead}]->获取Sip通道失败->{deviceId}-{channelId}->{JsonHelper.ToJson(rs)}");

                return null;
            }

            var tmpSipDevice = LibGB28181SipServer.Common.SipDevices.FindLast(x => x.DeviceId.Equals(deviceId));

            if (tmpSipDevice == null)
            {
                rs = new ResponseStruct()
                {
                    Code = ErrorNumber.Sip_DeviceNotExists,
                    Message = ErrorMessage.ErrorDic![ErrorNumber.Sip_DeviceNotExists],
                };
                Logger.Warn($"[{Common.LoggerHead}]->获取Sip通道失败->{deviceId}-{channelId}->{JsonHelper.ToJson(rs)}");

                return null;
            }

            var tmpSipChannel = tmpSipDevice.SipChannels.FindLast(x => x.DeviceId.Equals(channelId));
            if (tmpSipChannel == null)
            {
                rs = new ResponseStruct()
                {
                    Code = ErrorNumber.Sip_ChannelNotExists,
                    Message = ErrorMessage.ErrorDic![ErrorNumber.Sip_ChannelNotExists],
                };
                Logger.Warn($"[{Common.LoggerHead}]->获取Sip通道失败->{deviceId}-{channelId}->{JsonHelper.ToJson(rs)}");

                return null;
            }

            Logger.Debug(
                $"[{Common.LoggerHead}]->获取Sip通道成功->{deviceId}-{channelId}->{JsonHelper.ToJson(tmpSipChannel)}");

            return tmpSipChannel;
        }

        /// <summary>
        /// 通过DeviceId获取Device设备实例
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="rs"></param>
        /// <returns></returns>
        public static SipDevice GetSipDeviceListByDeviceId(string deviceId, out ResponseStruct rs)
        {
            rs = new ResponseStruct()
            {
                Code = ErrorNumber.None,
                Message = ErrorMessage.ErrorDic![ErrorNumber.None],
            };
            if (string.IsNullOrEmpty(deviceId))
            {
                rs = new ResponseStruct()
                {
                    Code = ErrorNumber.Sys_ParamsIsNotRight,
                    Message = ErrorMessage.ErrorDic![ErrorNumber.Sys_ParamsIsNotRight],
                };
                Logger.Warn($"[{Common.LoggerHead}]->获取Sip设备失败->{deviceId}->{JsonHelper.ToJson(rs)}");

                return null;
            }

            var ret = LibGB28181SipServer.Common.SipDevices.FindLast(x => x.DeviceId.Equals(deviceId));
            if (ret == null)
            {
                Logger.Warn($"[{Common.LoggerHead}]->获取Sip设备失败->{deviceId}->结果为空");
            }
            else
            {
                Logger.Debug($"[{Common.LoggerHead}]->获取Sip设备成功->{deviceId}->{JsonHelper.ToJson(ret)}");
            }

            return ret;
        }

        /// <summary>
        /// 获取所有Sip设备列表
        /// </summary>
        /// <param name="rs"></param>
        /// <returns></returns>
        public static List<SipDevice> GetSipDeviceList(out ResponseStruct rs)
        {
            rs = new ResponseStruct()
            {
                Code = ErrorNumber.None,
                Message = ErrorMessage.ErrorDic![ErrorNumber.None],
            };
            Logger.Info(
                $"[{Common.LoggerHead}]->获取Sip设备列表成功->{JsonHelper.ToJson(LibGB28181SipServer.Common.SipDevices.Count)}");
            return LibGB28181SipServer.Common.SipDevices;
        }
    }
}