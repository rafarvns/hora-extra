/**
 * Payload para sincronização de movimento de alta frequência.
 * Nomes de chaves curtos ('p' para posição e 'r' para rotação)
 * para otimização de banda em pacotes de socket.
 */
export interface IPlayerMovePayload {
    p: [number, number, number]; // [x, y, z]
    r: number;                   // Rotação Y (Yaw)
    f?: number;                  // Andar (1/2/3). Ausente = mantém o último conhecido.
}
