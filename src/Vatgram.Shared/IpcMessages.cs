using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Vatgram.Shared;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(HelloMessage), "hello")]
[JsonDerivedType(typeof(PrivateMessageEvent), "pm")]
[JsonDerivedType(typeof(RadioMessageEvent), "radio")]
[JsonDerivedType(typeof(BroadcastMessageEvent), "broadcast")]
[JsonDerivedType(typeof(SelcalEvent), "selcal")]
[JsonDerivedType(typeof(NetworkConnectedEvent), "net_connected")]
[JsonDerivedType(typeof(NetworkDisconnectedEvent), "net_disconnected")]
[JsonDerivedType(typeof(MetarReceivedEvent), "metar")]
[JsonDerivedType(typeof(AtisReceivedEvent), "atis")]
[JsonDerivedType(typeof(SendPrivateMessageCommand), "send_pm")]
[JsonDerivedType(typeof(SendRadioMessageCommand), "send_radio")]
[JsonDerivedType(typeof(RequestMetarCommand), "req_metar")]
[JsonDerivedType(typeof(RequestAtisCommand), "req_atis")]
[JsonDerivedType(typeof(RequestDisconnectCommand), "req_disconnect")]
[JsonDerivedType(typeof(SquawkIdentCommand), "squawk_ident")]
[JsonDerivedType(typeof(SetModeCCommand), "set_modec")]
public abstract record IpcMessage;

public sealed record HelloMessage(int ProtocolVersion, string PluginVersion) : IpcMessage;

public sealed record PrivateMessageEvent(string From, string Message) : IpcMessage;
public sealed record RadioMessageEvent(int[] Frequencies, string From, string Message) : IpcMessage;
public sealed record BroadcastMessageEvent(string From, string Message) : IpcMessage;
public sealed record SelcalEvent(int[] Frequencies, string From) : IpcMessage;
public sealed record NetworkConnectedEvent(string Cid, string Callsign, string TypeCode, string? SelcalCode, bool ObserverMode) : IpcMessage;
public sealed record NetworkDisconnectedEvent : IpcMessage;
public sealed record MetarReceivedEvent(string Metar) : IpcMessage;
public sealed record AtisReceivedEvent(string From, List<string> Lines) : IpcMessage;

public sealed record SendPrivateMessageCommand(string To, string Message) : IpcMessage;
public sealed record SendRadioMessageCommand(string Message) : IpcMessage;
public sealed record RequestMetarCommand(string Station) : IpcMessage;
public sealed record RequestAtisCommand(string Callsign) : IpcMessage;
public sealed record RequestDisconnectCommand : IpcMessage;
public sealed record SquawkIdentCommand : IpcMessage;
public sealed record SetModeCCommand(bool Enabled) : IpcMessage;
