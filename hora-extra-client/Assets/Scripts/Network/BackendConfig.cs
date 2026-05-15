namespace HoraExtra.Network
{
    /// <summary>
    /// Configuração centralizada de host/porta do backend.
    /// Editar <see cref="Host"/> aqui muda simultaneamente o REST (porta <see cref="HttpPort"/>)
    /// e o UDP (porta <see cref="UdpPort"/>).
    ///
    /// Para deploy remoto, atualize Host (ex.: "92.113.39.4"). Lembrar de:
    /// 1. No backend, garantir que o firewall/security group abre as portas 5000 (TCP) e 5001 (UDP).
    /// 2. No Unity Editor: Project Settings → Player → Other Settings → Configuration →
    ///    "Allow downloads over HTTP*" = "Always allowed" (caso contrário Unity bloqueia HTTP
    ///    para IPs não-loopback com erro "Insecure connection not allowed").
    /// </summary>
    public static class BackendConfig
    {
        /// <summary>Host/IP do backend. Use "127.0.0.1" pra local ou IP público pra remoto.</summary>
        public static string Host = "92.113.39.4";

        /// <summary>Porta HTTP REST do backend.</summary>
        public static int HttpPort = 5000;

        /// <summary>Porta UDP do backend.</summary>
        public static int UdpPort = 5001;

        /// <summary>URL base completa para chamadas REST.</summary>
        public static string ApiBaseUrl => $"http://{Host}:{HttpPort}/api";
    }
}
