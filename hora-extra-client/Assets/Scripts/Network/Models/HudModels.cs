using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HoraExtra.Network.Models
{
    /// <summary>
    /// Payload S→C de shift_clock: relógio do expediente da sala.
    /// Shape: { m: number, s: number, e: number, k: number, st: "idle" | "running" | "ended" }
    ///
    /// Não carrega timestamp de propósito: o cliente mede Δt local desde o recebimento e
    /// extrapola <c>M + K·Δt</c>, sem depender do relógio da máquina bater com o do servidor.
    /// </summary>
    [Serializable]
    public class ShiftClockPayload
    {
        /// <summary>Minuto de jogo atual (minutos desde 00:00, fracionário).</summary>
        [JsonProperty("m")]  public float  Minute;

        /// <summary>Minuto de início do expediente (480 = 08:00).</summary>
        [JsonProperty("s")]  public float  StartMinute;

        /// <summary>Minuto de fim do expediente (1080 = 18:00).</summary>
        [JsonProperty("e")]  public float  EndMinute;

        /// <summary>Minutos de jogo por segundo real. 0 quando o relógio está parado.</summary>
        [JsonProperty("k")]  public float  Rate;

        /// <summary>"idle", "running" ou "ended".</summary>
        [JsonProperty("st")] public string Status;
    }

    /// <summary>Entrada do roster: id do jogador + nome de exibição.</summary>
    [Serializable]
    public class RosterEntry
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("n")]  public string Name;
    }

    /// <summary>
    /// Payload S→C de room_roster: lista COMPLETA de jogadores da sala.
    /// Shape: { p: [{ id, n }] }. O cliente substitui a lista inteira a cada evento.
    /// </summary>
    [Serializable]
    public class RoomRosterPayload
    {
        [JsonProperty("p")] public List<RosterEntry> Players;
    }
}
