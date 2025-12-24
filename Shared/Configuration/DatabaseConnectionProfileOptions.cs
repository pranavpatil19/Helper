using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Shared.Configuration;

public sealed class SqlServerConnectionProfileOptions
{
    public string Server { get; set; } = "localhost";
    public int? Port { get; set; }
    public string Database { get; set; } = string.Empty;
    public bool TrustedConnection { get; set; } = true;
    public string? UserId { get; set; }
    public string? Password { get; set; }
    public bool TrustServerCertificate { get; set; } = true;
    public bool MultipleActiveResultSets { get; set; } = true;
    public bool Encrypt { get; set; }
    public string? ApplicationName { get; set; }

    public string BuildConnectionString()
    {
        if (string.IsNullOrWhiteSpace(Database))
        {
            throw new InvalidOperationException("SQL Server database name must be provided.");
        }

        var segments = new List<string>
        {
            $"Server={BuildServerValue()}",
            $"Database={Database}"
        };

        if (TrustedConnection)
        {
            segments.Add("Integrated Security=True");
        }
        else
        {
            segments.Add($"User ID={UserId ?? throw new InvalidOperationException("UserId is required when TrustedConnection is false.")}");
            segments.Add($"Password={Password ?? throw new InvalidOperationException("Password is required when TrustedConnection is false.")}");
        }

        if (TrustServerCertificate)
        {
            segments.Add("TrustServerCertificate=True");
        }

        if (MultipleActiveResultSets)
        {
            segments.Add("MultipleActiveResultSets=True");
        }

        if (Encrypt)
        {
            segments.Add("Encrypt=True");
        }

        if (!string.IsNullOrWhiteSpace(ApplicationName))
        {
            segments.Add($"Application Name={ApplicationName}");
        }

        return string.Join(";", segments);
    }

    private string BuildServerValue() =>
        Port is { } port ? $"{Server},{port.ToString(CultureInfo.InvariantCulture)}" : Server;
}

public sealed class OracleConnectionProfileOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1521;
    public string ServiceName { get; set; } = "xe";
    public string UserId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool Pooling { get; set; } = true;

    public string BuildConnectionString()
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            throw new InvalidOperationException("Oracle UserId must be provided.");
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            throw new InvalidOperationException("Oracle Password must be provided.");
        }

        var builder = new StringBuilder();
        builder.Append($"Data Source=//{Host}:{Port.ToString(CultureInfo.InvariantCulture)}/{ServiceName};");
        builder.Append($"User Id={UserId};");
        builder.Append($"Password={Password};");
        builder.Append($"Pooling={(Pooling ? "True" : "False")}");
        return builder.ToString();
    }
}

public sealed class PostgresConnectionProfileOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5432;
    public string Database { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool Pooling { get; set; } = true;
    public PostgresSslMode SslMode { get; set; } = PostgresSslMode.Prefer;

    public string BuildConnectionString()
    {
        if (string.IsNullOrWhiteSpace(Database))
        {
            throw new InvalidOperationException("PostgreSQL database name must be provided.");
        }

        if (string.IsNullOrWhiteSpace(Username))
        {
            throw new InvalidOperationException("PostgreSQL username must be provided.");
        }

        var segments = new List<string>
        {
            $"Host={Host}",
            $"Port={Port.ToString(CultureInfo.InvariantCulture)}",
            $"Database={Database}",
            $"Username={Username}",
            $"Password={Password}",
            $"Pooling={(Pooling ? "True" : "False")}",
            $"Ssl Mode={MapSslMode(SslMode)}"
        };

        return string.Join(";", segments);
    }

    private static string MapSslMode(PostgresSslMode mode) =>
        mode switch
        {
            PostgresSslMode.Disable => "Disable",
            PostgresSslMode.Allow => "Allow",
            PostgresSslMode.Prefer => "Prefer",
            PostgresSslMode.Require => "Require",
            PostgresSslMode.VerifyCA => "VerifyCA",
            PostgresSslMode.VerifyFull => "VerifyFull",
            _ => "Prefer"
        };
}

public enum PostgresSslMode
{
    Disable,
    Allow,
    Prefer,
    Require,
    VerifyCA,
    VerifyFull
}
