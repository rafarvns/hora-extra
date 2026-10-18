namespace HoraExtra.Network
{
    /// <summary>
    /// Configuração centralizada de host/porta do backend.
    /// Editar <see cref="Host"/> aqui muda simultaneamente o REST (porta <see cref="HttpPort"/>)
    /// e o UDP (porta <see cref="UdpPort"/>).
    ///
    /// O jogador também pode trocar o servidor em jogo (menu → Configurações → Servidor): a
    /// escolha é salva por <c>GameSettings.ServerPreset</c> e aplicada no boot via
    /// <see cref="ApplyPreset"/>. Sem escolha salva, vale o <see cref="Host"/> abaixo.
    ///
    /// Para deploy remoto, lembrar de:
    /// 1. No backend, garantir que o firewall/security group abre as portas 5000 (TCP) e 5001 (UDP).
    /// 2. No Unity Editor: Project Settings → Player → Other Settings → Configuration →
    ///    "Allow downloads over HTTP*" = "Always allowed" (caso contrário Unity bloqueia HTTP
    ///    para IPs não-loopback com erro "Insecure connection not allowed").
    /// </summary>
    public static class BackendConfig
    {
        /// <summary>Servidor selecionável no menu.</summary>
        public readonly struct ServerPresetInfo
        {
            public readonly string Id;
            public readonly string Label;
            public readonly string Host;

            public ServerPresetInfo(string id, string label, string host)
            {
                Id = id;
                Label = label;
                Host = host;
            }
        }

        /// <summary>Servidores conhecidos. O primeiro é o padrão de fábrica.</summary>
        public static readonly ServerPresetInfo[] Presets =
        {
            new ServerPresetInfo("homelab", "Homelab (rede local)", "192.168.10.151"),
            new ServerPresetInfo("demo",    "Demonstração (externo)", "187.127.55.122"),
            new ServerPresetInfo("local",   "Esta máquina", "127.0.0.1"),
        };

        /// <summary>Host/IP do backend. Use "127.0.0.1" pra local ou IP do servidor pra remoto.</summary>
        public static string Host = "192.168.10.151";  // homelab (so na rede local)
        // public static string Host = "187.127.55.122"; // build de demonstracao (acesso externo)
        // public static string Host = "127.0.0.1";      // backend rodando na propria maquina
        // public static string Host = "92.113.39.4";    // VPS

        /// <summary>Porta HTTP REST do backend.</summary>
        public static int HttpPort = 5000;

        /// <summary>Porta UDP do backend.</summary>
        public static int UdpPort = 5001;

        /// <summary>URL base completa para chamadas REST.</summary>
        public static string ApiBaseUrl => $"http://{Host}:{HttpPort}/api";

        /// <summary>Preset que corresponde ao <see cref="Host"/> atual, ou null se for um host avulso.</summary>
        public static ServerPresetInfo? CurrentPreset
        {
            get
            {
                foreach (var p in Presets)
                    if (p.Host == Host) return p;
                return null;
            }
        }

        /// <summary>
        /// Aplica o preset <paramref name="presetId"/>. Id vazio ou desconhecido mantém o
        /// <see cref="Host"/> atual. Vale para as PRÓXIMAS conexões — o SocketManager lê o host
        /// a cada (re)conexão.
        /// </summary>
        public static void ApplyPreset(string presetId)
        {
            if (string.IsNullOrEmpty(presetId)) return;
            foreach (var p in Presets)
            {
                if (p.Id != presetId) continue;
                Host = p.Host;
                return;
            }
        }
    }
}
