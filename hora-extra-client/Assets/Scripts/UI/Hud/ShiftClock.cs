using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
using HoraExtra.Characters;
using HoraExtra.Network.Models;

namespace HoraExtra.UI.Hud
{
    /// <summary>
    /// Relógio do expediente no cliente — espelho do ShiftClockService do servidor.
    ///
    /// Fluxo:
    ///   1. Quando a conexão está de pé e a introdução terminou (TaskSystemBridge.HoldTaskRequest
    ///      volta a false), envia shift_clock_request. O primeiro pedido da sala inicia o
    ///      expediente no servidor.
    ///   2. Cada shift_clock recebido vira a base; entre pacotes o minuto é extrapolado com
    ///      <c>M + K·Δt</c> (Δt local, sem depender do relógio do servidor).
    ///   3. Reenvia o pedido a cada <see cref="RESYNC_SECONDS"/> para corrigir drift.
    ///
    /// Sem servidor (ou servidor antigo sem o evento), fica em <see cref="ShiftState.Idle"/> e a
    /// HUD mostra "--:--".
    /// </summary>
    public class ShiftClock : MonoBehaviour
    {
        public enum ShiftState { Idle, Running, Ended }

        private const float RESYNC_SECONDS = 30f;
        private const float RETRY_SECONDS = 3f;

        /// <summary>Instância ativa (a HUD da cena). Pode ser null fora das cenas de jogo.</summary>
        public static ShiftClock Instance { get; private set; }

        /// <summary>Disparado quando o estado muda (Idle → Running → Ended).</summary>
        public event Action<ShiftState> OnStateChanged;

        /// <summary>Disparado uma vez por minuto de jogo (inteiro) que passa.</summary>
        public event Action<int> OnMinuteTick;

        public ShiftState State { get; private set; } = ShiftState.Idle;
        public float StartMinute { get; private set; } = 8 * 60;
        public float EndMinute { get; private set; } = 18 * 60;

        /// <summary>Minuto de jogo atual extrapolado (minutos desde 00:00).</summary>
        public float CurrentMinute
        {
            get
            {
                if (State != ShiftState.Running) return _baseMinute;
                float extrapolado = _baseMinute + _rate * (Time.unscaledTime - _baseRealTime);
                return Mathf.Min(extrapolado, EndMinute);
            }
        }

        /// <summary>Fração do expediente já decorrida (0..1).</summary>
        public float Progress01 => Mathf.InverseLerp(StartMinute, EndMinute, CurrentMinute);

        private float _baseMinute = 8 * 60;
        private float _baseRealTime;
        private float _rate;
        private float _nextRequestAt;
        private bool _receivedOnce;
        private int _lastTickMinute = -1;

        /// <summary>Formata minutos desde 00:00 como "HH:MM".</summary>
        public static string Format(float minute)
        {
            int total = Mathf.FloorToInt(minute);
            return $"{(total / 60) % 24:00}:{total % 60:00}";
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            SocketManager.Instance?.On(NetworkEvents.SHIFT_CLOCK, OnShiftClockReceived);
        }

        private void OnDisable()
        {
            SocketManager.Instance?.Off(NetworkEvents.SHIFT_CLOCK, OnShiftClockReceived);
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            TryRequest();

            if (State == ShiftState.Running)
            {
                int minuto = Mathf.FloorToInt(CurrentMinute);
                if (minuto != _lastTickMinute)
                {
                    _lastTickMinute = minuto;
                    OnMinuteTick?.Invoke(minuto);
                }

                // Chegou ao fim pela extrapolação: não espera o próximo resync para encerrar.
                if (CurrentMinute >= EndMinute)
                {
                    _baseMinute = EndMinute;
                    _rate = 0f;
                    SetState(ShiftState.Ended);
                }
            }
        }

        private void TryRequest()
        {
            var socket = SocketManager.Instance;
            if (socket == null || !socket.IsConnected) return;

            // A introdução segura as tarefas; o expediente começa junto com elas.
            if (TaskSystemBridge.HoldTaskRequest) return;

            if (Time.unscaledTime < _nextRequestAt) return;

            // Antes da primeira resposta, tenta de novo rápido (UDP pode perder o pacote).
            _nextRequestAt = Time.unscaledTime + (_receivedOnce ? RESYNC_SECONDS : RETRY_SECONDS);
            if (State == ShiftState.Ended) return;

            socket.Emit(NetworkEvents.SHIFT_CLOCK_REQUEST, new { });
        }

        private void OnShiftClockReceived(JToken data)
        {
            ShiftClockPayload payload;
            try
            {
                payload = data.ToObject<ShiftClockPayload>();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NETWORK] Falha ao parsear shift_clock: {ex.Message} | raw: {data}");
                return;
            }
            if (payload == null) return;

            if (!_receivedOnce)
                Debug.Log($"[UI] ShiftClock — expediente sincronizado: {Format(payload.Minute)} ({payload.Status}, {payload.Rate:0.00} min/s).");

            _receivedOnce = true;
            _nextRequestAt = Time.unscaledTime + RESYNC_SECONDS;

            StartMinute = payload.StartMinute > 0 ? payload.StartMinute : StartMinute;
            EndMinute = payload.EndMinute > 0 ? payload.EndMinute : EndMinute;
            _baseMinute = payload.Minute;
            _rate = payload.Rate;
            _baseRealTime = Time.unscaledTime;

            switch (payload.Status)
            {
                case "running": SetState(ShiftState.Running); break;
                case "ended":   SetState(ShiftState.Ended); break;
                default:        SetState(ShiftState.Idle); break;
            }
        }

        private void SetState(ShiftState state)
        {
            if (State == state) return;
            State = state;
            Debug.Log($"[UI] ShiftClock — estado: {state} ({Format(CurrentMinute)}).");
            OnStateChanged?.Invoke(state);
        }
    }
}
