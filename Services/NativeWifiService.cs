using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using WiFiChecker.Models;

namespace WiFiChecker.Services
{
    /// <summary>
    /// Windows Native Wifi API (wlanapi.dll) ラッパー
    /// netsh の出力言語・文字コード・OSバージョン差異に依存せず、確実にWi-Fi情報を取得
    /// </summary>
    public static class NativeWifiService
    {
        private const uint WLAN_CLIENT_VERSION_VISTA = 2;

        public static WifiInfo? GetCurrentWifiInfo()
        {
            IntPtr clientHandle = IntPtr.Zero;
            try
            {
                uint result = WlanOpenHandle(WLAN_CLIENT_VERSION_VISTA, IntPtr.Zero, out _, out clientHandle);
                if (result != 0 || clientHandle == IntPtr.Zero)
                {
                    return null;
                }

                result = WlanEnumInterfaces(clientHandle, IntPtr.Zero, out IntPtr interfaceListPtr);
                if (result != 0 || interfaceListPtr == IntPtr.Zero)
                {
                    return null;
                }

                try
                {
                    var interfaceList = Marshal.PtrToStructure<WLAN_INTERFACE_INFO_LIST>(interfaceListPtr);
                    int offset = Marshal.OffsetOf<WLAN_INTERFACE_INFO_LIST>("InterfaceInfo").ToInt32();
                    int structSize = Marshal.SizeOf<WLAN_INTERFACE_INFO>();

                    for (int i = 0; i < interfaceList.dwNumberOfItems; i++)
                    {
                        IntPtr currentPtr = new IntPtr(interfaceListPtr.ToInt64() + offset + (i * structSize));
                        var info = Marshal.PtrToStructure<WLAN_INTERFACE_INFO>(currentPtr);

                        if (info.isState == WLAN_INTERFACE_STATE.wlan_interface_state_connected)
                        {
                            var wifiInfo = ExtractConnectedInfo(clientHandle, info);
                            if (wifiInfo != null)
                            {
                                return wifiInfo;
                            }
                        }
                    }

                    // 接続中のインターフェイスがなければ、先頭のインターフェイス情報を切断状態で返す
                    if (interfaceList.dwNumberOfItems > 0)
                    {
                        IntPtr firstPtr = new IntPtr(interfaceListPtr.ToInt64() + offset);
                        var firstInfo = Marshal.PtrToStructure<WLAN_INTERFACE_INFO>(firstPtr);
                        return new WifiInfo
                        {
                            IsConnected = false,
                            InterfaceName = firstInfo.strInterfaceDescription,
                            LastRefreshed = DateTime.Now
                        };
                    }
                }
                finally
                {
                    WlanFreeMemory(interfaceListPtr);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"NativeWifiService エラー: {ex.Message}");
            }
            finally
            {
                if (clientHandle != IntPtr.Zero)
                {
                    WlanCloseHandle(clientHandle, IntPtr.Zero);
                }
            }

            return null;
        }

        private static WifiInfo? ExtractConnectedInfo(IntPtr clientHandle, WLAN_INTERFACE_INFO iface)
        {
            var guid = iface.InterfaceGuid;
            uint result = WlanQueryInterface(
                clientHandle,
                ref guid,
                WLAN_INTF_OPCODE.wlan_intf_opcode_current_connection,
                IntPtr.Zero,
                out uint dataSize,
                out IntPtr dataPtr,
                out _);

            int requiredSize = Marshal.SizeOf<WLAN_CONNECTION_ATTRIBUTES>();
            if (result != 0 || dataPtr == IntPtr.Zero || dataSize < requiredSize)
            {
                if (dataPtr != IntPtr.Zero)
                {
                    WlanFreeMemory(dataPtr);
                }
                return null;
            }

            try
            {
                var conn = Marshal.PtrToStructure<WLAN_CONNECTION_ATTRIBUTES>(dataPtr);

                var wifiInfo = new WifiInfo
                {
                    IsConnected = true,
                    InterfaceName = iface.strInterfaceDescription,
                    LastRefreshed = DateTime.Now
                };

                // 1. SSID
                if (conn.wlanAssociationAttributes.dot11Ssid.uSSIDLength > 0)
                {
                    byte[] rawSsid = conn.wlanAssociationAttributes.dot11Ssid.ucSSID;
                    int length = (int)Math.Min(conn.wlanAssociationAttributes.dot11Ssid.uSSIDLength, (uint)(rawSsid?.Length ?? 0));
                    if (rawSsid != null && length > 0)
                    {
                        wifiInfo.Ssid = Encoding.UTF8.GetString(rawSsid, 0, length);
                    }
                }

                // 2. BSSID (MAC)
                byte[]? bssidBytes = conn.wlanAssociationAttributes.dot11Bssid;
                if (bssidBytes != null && bssidBytes.Length >= 6)
                {
                    wifiInfo.Bssid = string.Format("{0:X2}:{1:X2}:{2:X2}:{3:X2}:{4:X2}:{5:X2}",
                        bssidBytes[0], bssidBytes[1], bssidBytes[2], bssidBytes[3], bssidBytes[4], bssidBytes[5]);
                }

                // 3. 電波強度 (Signal Quality & dBm)
                wifiInfo.SignalQuality = (int)conn.wlanAssociationAttributes.wlanSignalQuality;
                wifiInfo.SignalDbm = (wifiInfo.SignalQuality / 2) - 100;

                // 4. リンク速度 (kbps -> Mbps)
                wifiInfo.LinkSpeedRxMbps = conn.wlanAssociationAttributes.ulRxRate / 1000;
                wifiInfo.LinkSpeedTxMbps = conn.wlanAssociationAttributes.ulTxRate / 1000;

                // 5. PHY規格 (Wi-Fi 4 / 5 / 6 / 6E / 7 等)
                wifiInfo.PhyType = ConvertPhyType(conn.wlanAssociationAttributes.dot11PhyType);

                // 6. 認証 & 暗号化
                wifiInfo.Authentication = ConvertAuthAlgorithm(conn.wlanSecurityAttributes.dot11AuthAlgorithm);
                wifiInfo.Cipher = ConvertCipherAlgorithm(conn.wlanSecurityAttributes.dot11CipherAlgorithm);

                // 7. 周波数・チャンネル・RSSIの詳細を BssList から取得
                EnrichBssListInfo(clientHandle, guid, wifiInfo, conn.wlanAssociationAttributes.dot11Ssid);

                return wifiInfo;
            }
            finally
            {
                WlanFreeMemory(dataPtr);
            }
        }

        private static void EnrichBssListInfo(IntPtr clientHandle, Guid guid, WifiInfo wifiInfo, DOT11_SSID ssid)
        {
            IntPtr ssidPtr = IntPtr.Zero;
            try
            {
                ssidPtr = Marshal.AllocHGlobal(Marshal.SizeOf<DOT11_SSID>());
                Marshal.StructureToPtr(ssid, ssidPtr, false);

                uint result = WlanGetNetworkBssList(
                    clientHandle,
                    ref guid,
                    ssidPtr,
                    DOT11_BSS_TYPE.dot11_BSS_type_any,
                    true,
                    IntPtr.Zero,
                    out IntPtr bssListPtr);

                Marshal.FreeHGlobal(ssidPtr);
                ssidPtr = IntPtr.Zero;

                if (result != 0 || bssListPtr == IntPtr.Zero)
                {
                    // SSID指定なしで再取得を試行
                    result = WlanGetNetworkBssList(
                        clientHandle,
                        ref guid,
                        IntPtr.Zero,
                        DOT11_BSS_TYPE.dot11_BSS_type_any,
                        true,
                        IntPtr.Zero,
                        out bssListPtr);
                }

                if (result == 0 && bssListPtr != IntPtr.Zero)
                {
                    try
                    {
                        var bssList = Marshal.PtrToStructure<WLAN_BSS_LIST>(bssListPtr);
                        int offset = Marshal.OffsetOf<WLAN_BSS_LIST>("wlanBssEntries").ToInt32();
                        int entrySize = Marshal.SizeOf<WLAN_BSS_ENTRY>();

                        // dwNumberOfItems が異常値でないこと、dwTotalSize を超えないことをチェック
                        uint maxItems = Math.Min(bssList.dwNumberOfItems, 256);
                        for (int i = 0; i < maxItems; i++)
                        {
                            long itemOffset = offset + (i * entrySize);
                            if (itemOffset + entrySize > bssList.dwTotalSize)
                            {
                                break;
                            }

                            IntPtr entryPtr = new IntPtr(bssListPtr.ToInt64() + itemOffset);
                            var entry = Marshal.PtrToStructure<WLAN_BSS_ENTRY>(entryPtr);

                            if (entry.dot11Bssid != null && entry.dot11Bssid.Length >= 6)
                            {
                                string bssid = string.Format("{0:X2}:{1:X2}:{2:X2}:{3:X2}:{4:X2}:{5:X2}",
                                    entry.dot11Bssid[0], entry.dot11Bssid[1], entry.dot11Bssid[2],
                                    entry.dot11Bssid[3], entry.dot11Bssid[4], entry.dot11Bssid[5]);

                                if (string.Equals(bssid, wifiInfo.Bssid, StringComparison.OrdinalIgnoreCase))
                                {
                                    // RSSI (dBm)
                                    if (entry.lRssi < 0)
                                    {
                                        wifiInfo.SignalDbm = (int)entry.lRssi;
                                    }

                                    // 周波数 (kHz -> GHz / チャンネル)
                                    if (entry.ulChCenterFrequency > 0)
                                    {
                                        double freqMhz = entry.ulChCenterFrequency / 1000.0;
                                        wifiInfo.FrequencyGhz = freqMhz / 1000.0;
                                        (wifiInfo.Band, wifiInfo.Channel) = CalculateBandAndChannel(freqMhz);
                                    }
                                    break;
                                }
                            }
                        }
                    }
                    finally
                    {
                        WlanFreeMemory(bssListPtr);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EnrichBssListInfo エラー: {ex.Message}");
            }
            finally
            {
                if (ssidPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(ssidPtr);
                }
            }
        }

        private static (string Band, int Channel) CalculateBandAndChannel(double freqMhz)
        {
            // 2.4 GHz 帯 (2412 ~ 2484 MHz)
            if (freqMhz >= 2400 && freqMhz <= 2500)
            {
                if (Math.Abs(freqMhz - 2484) < 1) return ("2.4 GHz", 14);
                int ch = (int)Math.Round((freqMhz - 2407) / 5.0);
                return ("2.4 GHz", Math.Max(1, Math.Min(14, ch)));
            }

            // 5 GHz 帯 (5150 ~ 5885 MHz)
            if (freqMhz >= 5000 && freqMhz <= 5900)
            {
                int ch = (int)Math.Round((freqMhz - 5000) / 5.0);
                return ("5 GHz", ch);
            }

            // 6 GHz 帯 (5945 ~ 7125 MHz)
            if (freqMhz >= 5925 && freqMhz <= 7125)
            {
                int ch = (int)Math.Round((freqMhz - 5950) / 5.0);
                return ("6 GHz", ch);
            }

            return ("Unknown", 0);
        }

        private static string ConvertPhyType(DOT11_PHY_TYPE phyType)
        {
            return phyType switch
            {
                DOT11_PHY_TYPE.dot11_phy_type_eht => "802.11be (Wi-Fi 7)",
                DOT11_PHY_TYPE.dot11_phy_type_he => "802.11ax (Wi-Fi 6 / 6E)",
                DOT11_PHY_TYPE.dot11_phy_type_vht => "802.11ac (Wi-Fi 5)",
                DOT11_PHY_TYPE.dot11_phy_type_ht => "802.11n (Wi-Fi 4)",
                DOT11_PHY_TYPE.dot11_phy_type_erp => "802.11g (Wi-Fi 3)",
                DOT11_PHY_TYPE.dot11_phy_type_hrdsss => "802.11b",
                DOT11_PHY_TYPE.dot11_phy_type_ofdm => "802.11a",
                DOT11_PHY_TYPE.dot11_phy_type_dmg => "802.11ad",
                _ => phyType.ToString().Replace("dot11_phy_type_", "")
            };
        }

        private static string ConvertAuthAlgorithm(DOT11_AUTH_ALGORITHM auth)
        {
            return auth switch
            {
                DOT11_AUTH_ALGORITHM.DOT11_AUTH_ALGO_80211_OPEN => "Open",
                DOT11_AUTH_ALGORITHM.DOT11_AUTH_ALGO_80211_SHARED_KEY => "Shared Key",
                DOT11_AUTH_ALGORITHM.DOT11_AUTH_ALGO_WPA => "WPA-Enterprise",
                DOT11_AUTH_ALGORITHM.DOT11_AUTH_ALGO_WPA_PSK => "WPA-Personal",
                DOT11_AUTH_ALGORITHM.DOT11_AUTH_ALGO_RSNA => "WPA2-Enterprise",
                DOT11_AUTH_ALGORITHM.DOT11_AUTH_ALGO_RSNA_PSK => "WPA2-Personal",
                DOT11_AUTH_ALGORITHM.DOT11_AUTH_ALGO_WPA3 => "WPA3-Enterprise",
                DOT11_AUTH_ALGORITHM.DOT11_AUTH_ALGO_WPA3_SAE => "WPA3-Personal (SAE)",
                DOT11_AUTH_ALGORITHM.DOT11_AUTH_ALGO_OWE => "Enhanced Open (OWE)",
                _ => auth.ToString().Replace("DOT11_AUTH_ALGO_", "")
            };
        }

        private static string ConvertCipherAlgorithm(DOT11_CIPHER_ALGORITHM cipher)
        {
            return cipher switch
            {
                DOT11_CIPHER_ALGORITHM.DOT11_CIPHER_ALGO_NONE => "None",
                DOT11_CIPHER_ALGORITHM.DOT11_CIPHER_ALGO_CCMP => "CCMP (AES)",
                DOT11_CIPHER_ALGORITHM.DOT11_CIPHER_ALGO_TKIP => "TKIP",
                DOT11_CIPHER_ALGORITHM.DOT11_CIPHER_ALGO_GCMP => "GCMP",
                DOT11_CIPHER_ALGORITHM.DOT11_CIPHER_ALGO_GCMP_256 => "GCMP-256",
                DOT11_CIPHER_ALGORITHM.DOT11_CIPHER_ALGO_CCMP_256 => "CCMP-256",
                DOT11_CIPHER_ALGORITHM.DOT11_CIPHER_ALGO_WEP40 => "WEP-40",
                DOT11_CIPHER_ALGORITHM.DOT11_CIPHER_ALGO_WEP104 => "WEP-104",
                _ => cipher.ToString().Replace("DOT11_CIPHER_ALGO_", "")
            };
        }

        #region P/Invoke Structures and Methods

        [DllImport("wlanapi.dll", SetLastError = true)]
        private static extern uint WlanOpenHandle(
            uint dwClientVersion,
            IntPtr pReserved,
            out uint pdwNegotiatedVersion,
            out IntPtr phClientHandle);

        [DllImport("wlanapi.dll", SetLastError = true)]
        private static extern uint WlanCloseHandle(
            IntPtr hClientHandle,
            IntPtr pReserved);

        [DllImport("wlanapi.dll", SetLastError = true)]
        private static extern uint WlanEnumInterfaces(
            IntPtr hClientHandle,
            IntPtr pReserved,
            out IntPtr ppInterfaceList);

        [DllImport("wlanapi.dll", SetLastError = true)]
        private static extern uint WlanQueryInterface(
            IntPtr hClientHandle,
            [In] ref Guid pInterfaceGuid,
            WLAN_INTF_OPCODE OpCode,
            IntPtr pReserved,
            out uint pdwDataSize,
            out IntPtr ppData,
            out WLAN_OPCODE_VALUE_TYPE pWlanOpCodeValueType);

        [DllImport("wlanapi.dll", SetLastError = true)]
        private static extern uint WlanGetNetworkBssList(
            IntPtr hClientHandle,
            [In] ref Guid pInterfaceGuid,
            IntPtr pDot11Ssid,
            DOT11_BSS_TYPE dot11BssType,
            [MarshalAs(UnmanagedType.Bool)] bool bSecurityEnabled,
            IntPtr pReserved,
            out IntPtr ppWlanBssList);

        [DllImport("wlanapi.dll", SetLastError = true)]
        private static extern void WlanFreeMemory(IntPtr pMemory);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WLAN_INTERFACE_INFO
        {
            public Guid InterfaceGuid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strInterfaceDescription;
            public WLAN_INTERFACE_STATE isState;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WLAN_INTERFACE_INFO_LIST
        {
            public uint dwNumberOfItems;
            public uint dwIndex;
            public WLAN_INTERFACE_INFO InterfaceInfo;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WLAN_CONNECTION_ATTRIBUTES
        {
            public WLAN_INTERFACE_STATE isState;
            public uint wlanConnectionMode;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strProfileName;
            public WLAN_ASSOCIATION_ATTRIBUTES wlanAssociationAttributes;
            public WLAN_SECURITY_ATTRIBUTES wlanSecurityAttributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WLAN_ASSOCIATION_ATTRIBUTES
        {
            public DOT11_SSID dot11Ssid;
            public DOT11_BSS_TYPE dot11BssType;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            public byte[] dot11Bssid;
            public DOT11_PHY_TYPE dot11PhyType;
            public uint uDot11PhyIndex;
            public uint wlanSignalQuality;
            public uint ulRxRate;
            public uint ulTxRate;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WLAN_SECURITY_ATTRIBUTES
        {
            [MarshalAs(UnmanagedType.Bool)]
            public bool bSecurityEnabled;
            [MarshalAs(UnmanagedType.Bool)]
            public bool bOneXEnabled;
            public DOT11_AUTH_ALGORITHM dot11AuthAlgorithm;
            public DOT11_CIPHER_ALGORITHM dot11CipherAlgorithm;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DOT11_SSID
        {
            public uint uSSIDLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] ucSSID;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WLAN_BSS_LIST
        {
            public uint dwTotalSize;
            public uint dwNumberOfItems;
            public WLAN_BSS_ENTRY wlanBssEntries;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WLAN_BSS_ENTRY
        {
            public DOT11_SSID dot11Ssid;
            public uint uPhyId;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            public byte[] dot11Bssid;
            public DOT11_BSS_TYPE dot11BssType;
            public DOT11_PHY_TYPE dot11BssPhyType;
            public int lRssi;
            public uint uLinkQuality;
            [MarshalAs(UnmanagedType.Bool)]
            public bool bInRegDomain;
            public ushort usBeaconPeriod;
            public ulong ullTimestamp;
            public ulong ullHostTimestamp;
            public ushort usCapabilityInformation;
            public uint ulChCenterFrequency;
            public WLAN_RATE_SET wlanRateSet;
            public uint ulIeOffset;
            public uint ulIeSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WLAN_RATE_SET
        {
            public uint uRateSetLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 126)]
            public ushort[] usRateSet;
        }

        private enum WLAN_INTERFACE_STATE : uint
        {
            wlan_interface_state_not_ready = 0,
            wlan_interface_state_connected = 1,
            wlan_interface_state_ad_hoc_network_formed = 2,
            wlan_interface_state_disconnecting = 3,
            wlan_interface_state_disconnected = 4,
            wlan_interface_state_associating = 5,
            wlan_interface_state_discovering = 6,
            wlan_interface_state_authenticating = 7
        }

        private enum WLAN_INTF_OPCODE : uint
        {
            wlan_intf_opcode_autoconf_start = 0x000000000,
            wlan_intf_opcode_connection_mode,
            wlan_intf_opcode_radio_state,
            wlan_intf_opcode_current_connection,
            wlan_intf_opcode_supported_infrastructure_auth_cipher_pairs,
            wlan_intf_opcode_supported_adhoc_auth_cipher_pairs,
            wlan_intf_opcode_supported_country_or_region_string_list,
            wlan_intf_opcode_current_operation_mode,
            wlan_intf_opcode_supported_safe_mode,
            wlan_intf_opcode_certified_safe_mode,
            wlan_intf_opcode_hosted_network_capable,
            wlan_intf_opcode_management_frame_protection_capable,
            wlan_intf_opcode_autoconf_end = 0x0fffffff
        }

        private enum WLAN_OPCODE_VALUE_TYPE : uint
        {
            wlan_opcode_value_type_query_only = 0,
            wlan_opcode_value_type_set_by_group_policy,
            wlan_opcode_value_type_set_by_user,
            wlan_opcode_value_type_invalid
        }

        private enum DOT11_BSS_TYPE : uint
        {
            dot11_BSS_type_infrastructure = 1,
            dot11_BSS_type_independent = 2,
            dot11_BSS_type_any = 3
        }

        private enum DOT11_PHY_TYPE : uint
        {
            dot11_phy_type_unknown = 0,
            dot11_phy_type_any = 0,
            dot11_phy_type_fhss = 1,
            dot11_phy_type_dsss = 2,
            dot11_phy_type_irbaseband = 3,
            dot11_phy_type_ofdm = 4,
            dot11_phy_type_hrdsss = 5,
            dot11_phy_type_erp = 6,
            dot11_phy_type_ht = 7,
            dot11_phy_type_vht = 8,
            dot11_phy_type_dmg = 9,
            dot11_phy_type_he = 10,
            dot11_phy_type_eht = 11
        }

        private enum DOT11_AUTH_ALGORITHM : uint
        {
            DOT11_AUTH_ALGO_80211_OPEN = 1,
            DOT11_AUTH_ALGO_80211_SHARED_KEY = 2,
            DOT11_AUTH_ALGO_WPA = 3,
            DOT11_AUTH_ALGO_WPA_PSK = 4,
            DOT11_AUTH_ALGO_WPA_NONE = 5,
            DOT11_AUTH_ALGO_RSNA = 6,
            DOT11_AUTH_ALGO_RSNA_PSK = 7,
            DOT11_AUTH_ALGO_WPA3 = 8,
            DOT11_AUTH_ALGO_WPA3_ENT_192 = 9,
            DOT11_AUTH_ALGO_WPA3_SAE = 10,
            DOT11_AUTH_ALGO_OWE = 11,
            DOT11_AUTH_ALGO_WPA3_ENT = 12
        }

        private enum DOT11_CIPHER_ALGORITHM : uint
        {
            DOT11_CIPHER_ALGO_NONE = 0x00,
            DOT11_CIPHER_ALGO_WEP40 = 0x01,
            DOT11_CIPHER_ALGO_TKIP = 0x02,
            DOT11_CIPHER_ALGO_CCMP = 0x04,
            DOT11_CIPHER_ALGO_WEP104 = 0x05,
            DOT11_CIPHER_ALGO_BIP = 0x06,
            DOT11_CIPHER_ALGO_GCMP = 0x08,
            DOT11_CIPHER_ALGO_GCMP_256 = 0x09,
            DOT11_CIPHER_ALGO_CCMP_256 = 0x0a,
            DOT11_CIPHER_ALGO_BIP_GMAC_128 = 0x0b,
            DOT11_CIPHER_ALGO_BIP_GMAC_256 = 0x0c,
            DOT11_CIPHER_ALGO_BIP_CMAC_256 = 0x0d,
            DOT11_CIPHER_ALGO_WPA_USE_GROUP = 0x100,
            DOT11_CIPHER_ALGO_RSN_USE_GROUP = 0x100,
            DOT11_CIPHER_ALGO_WEP = 0x101
        }

        #endregion
    }
}
