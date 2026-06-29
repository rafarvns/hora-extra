using System;
using System.Threading.Tasks;
using UnityEngine;
using HoraExtra.Network.Models;

namespace HoraExtra.Network.Rest.Services
{
    /**
     * Serviço responsável pela comunicação com os endpoints de autenticação do backend.
     */
    public class AuthService
    {
        private const string RegisterEndpoint = "/auth/register";
        private const string LoginEndpoint = "/auth/login";

        /// <summary>
        /// Realiza o cadastro de um novo jogador.
        /// </summary>
        public async Task<ApiResponse<AuthData>> Register(string nome, string email, string senha)
        {
            try
            {
                var requestBody = new RegisterRequest
                {
                    Nome = nome,
                    Email = email,
                    Senha = senha
                };

                Debug.Log($"[AuthService] Enviando solicitação de cadastro para {email}...");
                var response = await ApiClient.Post<AuthData>(RegisterEndpoint, requestBody);

                if (response.Success)
                {
                    Debug.Log($"[AuthService] Cadastro realizado com sucesso! Bem-vindo, {response.Data.Jogador.Nome}");
                }
                else
                {
                    Debug.LogWarning($"[AuthService] Falha no cadastro: {response.Error.Message}");
                }

                return response;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AuthService] Erro inesperado ao registrar: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Realiza o login de um jogador existente.
        /// </summary>
        public async Task<ApiResponse<AuthData>> Login(string email, string senha)
        {
            try
            {
                var requestBody = new LoginRequest
                {
                    Email = email,
                    Senha = senha
                };

                Debug.Log($"[AuthService] Enviando solicitação de login para {email}...");
                var response = await ApiClient.Post<AuthData>(LoginEndpoint, requestBody);

                if (response.Success)
                {
                    Debug.Log($"[AuthService] Login bem-sucedido! Token recebido.");
                }
                else
                {
                    Debug.LogWarning($"[AuthService] Falha no login: {response.Error.Message}");
                }

                return response;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AuthService] Erro inesperado ao logar: {ex.Message}");
                return null;
            }
        }
    }
}
