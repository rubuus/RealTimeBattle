using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace api.Models
{
    // User 관리 모델
    public class User
    {
        public int Id { get; set; }
        public string State { get; private set; } = "exist";
        public string AccountId { get; private set; } = string.Empty;
        public string PasswordHash { get; private set; } = string.Empty;
        public string PasswordSalt { get; private set; } = string.Empty;
        public string Nickname { get; private set; } = string.Empty;
        public int ProfileImage { get; set; } = 0;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime CreatedTime { get; private set; } = DateTime.Now;
        public DateTime UpdatedTime { get; private set; } = DateTime.Now;
        

        private User() {}
        
        public User(string accountId, string password, string nickname)
        {
            AccountId = accountId;
            ChangePassword(password);
            Nickname = nickname;
        }

        public bool CheckState() => State == "exist";

        public void ChangeState(string newState)
        {
            State = newState;
            UpdateTimestamp();
        }

        public void ChangePassword(string newPassword)
        {
            if (!CheckState()) return;

            PasswordSalt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            PasswordHash = ComputeHash(newPassword, PasswordSalt);
            UpdateTimestamp();
        }

        public void ChangeNickname(string newNickname)
        {
            if (!CheckState()) return;

            Nickname = newNickname;
            UpdateTimestamp();
        }

        public void ChangeProfileImage(int idx)
        {
            ProfileImage = idx;
        }

        public void ChangeRefreshToken(string token)
        {
            RefreshToken = token;
        }

        // 이벤트 발생 시, 시간 업데이트
        private void UpdateTimestamp()
        {
            UpdatedTime = DateTime.Now;
        }

        // 비밀번호 확인
        public bool VerifyPassword(string inputPassword)
        {
            return ComputeHash(inputPassword, PasswordSalt) == PasswordHash;
        }

        // 비밀번호 + salt를 sha256으로 byte return
        public string ComputeHash(string input, string salt)
        {
            using var sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input + salt));
            return Convert.ToBase64String(bytes);
        }
    }
}