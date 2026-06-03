using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Downloader.Extensions;

namespace Downloader.Test.HelperTests;

public class ExceptionHelperTest
{
    [Fact]
    public void HasSourceFromThisNamespaceTest()
    {
        // arrange
        Exception exception = ExceptionThrower.GetException();
        string exceptionSource = exception.Source;
        string currentNamespace = "Downloader.Test";

        // act
        bool hasThisNamespace = exception.HasSource(currentNamespace);

        // assert
        Assert.True(hasThisNamespace,
            $"Exception.Source: {exceptionSource}, CurrentNamespace: {currentNamespace}");
    }

    [Fact]
    public void HasSourceFromNonOccurrenceNamespaceTest()
    {
        // arrange
        Exception exception = ExceptionThrower.GetException();

        // act
        bool hasSocketsNamespace = exception.HasSource("System.Net.Sockets");
        bool hasSecurityNamespace = exception.HasSource("System.Net.Security");

        // assert
        Assert.False(hasSocketsNamespace);
        Assert.False(hasSecurityNamespace);
    }

    [Fact]
    public void HasTypeOfWebExceptionTest()
    {
        // arrange
        Exception exception = ExceptionThrower.GetWebException();

        // act
        bool hasTypeOfWebExp = exception.HasTypeOf(typeof(WebException));

        // assert
        Assert.True(hasTypeOfWebExp);
    }

    [Fact]
    public void HasTypeOfInnerExceptionsTest()
    {
        // arrange
        Exception exception = ExceptionThrower.GetWebException();

        // act
        bool hasTypeOfMultipleTypes = exception.HasTypeOf(typeof(DivideByZeroException),
            typeof(ArgumentNullException), typeof(HttpRequestException));

        // assert
        Assert.True(hasTypeOfMultipleTypes);
    }

    [Fact]
    public void HasTypeOfNonOccurrenceExceptionsTest()
    {
        // arrange
        Exception exception = ExceptionThrower.GetWebException();

        // act
        bool hasTypeOfMultipleTypes = exception.HasTypeOf(typeof(DivideByZeroException),
            typeof(ArgumentNullException), typeof(InvalidCastException));

        // assert
        Assert.False(hasTypeOfMultipleTypes);
    }

    [Fact]
    public void IsMomentumErrorTestWhenNoWebException()
    {
        // arrange
        Exception exception = ExceptionThrower.GetException();

        // act
        bool isMomentumError = exception.IsMomentumError();

        // assert
        Assert.False(isMomentumError);
    }

    [Fact]
    public void IsMomentumErrorTestOnWebException()
    {
        // arrange
        Exception exception = ExceptionThrower.GetWebException();

        // act
        bool isMomentumError = exception.IsMomentumError();

        // assert
        Assert.True(isMomentumError);
    }

    // issue #226: 428 (used by some CDNs as a per-client concurrency throttle) must be retryable.
    // The exception is constructed (not thrown), so Source is null — representative of AOT/trimmed
    // builds where Exception.Source is empty. Classification must therefore rely on the status code.
    [Fact]
    public void IsMomentumErrorIsTrueFor428PreconditionRequiredWithoutSource()
    {
        // arrange
        HttpRequestException exception =
            new("throttled", inner: null, statusCode: HttpStatusCode.PreconditionRequired);

        // assert
        Assert.Null(exception.Source); // mirrors AOT, where Source is not populated
        Assert.True(exception.IsMomentumError());
    }

    // issue #226: a transport-level HttpRequestException (no HTTP status: reset/DNS/TLS/timeout)
    // is transient and must be retryable even when Source is empty (AOT/trimming).
    [Fact]
    public void IsMomentumErrorIsTrueForTransportHttpRequestExceptionWithoutSource()
    {
        // arrange
        HttpRequestException exception = new("connection reset");

        // assert
        Assert.Null(exception.StatusCode);
        Assert.Null(exception.Source);
        Assert.True(exception.IsMomentumError());
    }

    // Genuinely fatal HTTP errors must stay non-retryable regardless of Source being empty.
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)] // 500
    [InlineData(HttpStatusCode.BadGateway)]          // 502
    [InlineData(HttpStatusCode.BadRequest)]          // 400
    [InlineData(HttpStatusCode.Unauthorized)]        // 401
    [InlineData(HttpStatusCode.Forbidden)]           // 403
    [InlineData(HttpStatusCode.NotFound)]            // 404
    public void IsMomentumErrorIsFalseForPermanentHttpErrorsWithoutSource(HttpStatusCode statusCode)
    {
        // arrange
        HttpRequestException exception = new("fatal", inner: null, statusCode: statusCode);

        // assert
        Assert.False(exception.IsMomentumError());
    }

    // Transient transport / teardown exception types must be retryable purely by type.
    [Fact]
    public void IsMomentumErrorIsTrueForTransientTransportExceptionTypes()
    {
        Assert.True(new SocketException().IsMomentumError());
        Assert.True(new IOException("reset").IsMomentumError());                 // includes HttpIOException
        Assert.True(new TaskCanceledException("read timeout").IsMomentumError()); // timeout
        Assert.True(new ObjectDisposedException("stream").IsMomentumError());     // stream closed on timeout
        Assert.True(new WebException("timed out", WebExceptionStatus.Timeout).IsMomentumError());
    }

    [Fact]
    public void IsMomentumErrorIsFalseForNonTransientWebExceptionStatus()
    {
        Assert.False(new WebException("blocked", WebExceptionStatus.TrustFailure).IsMomentumError());
    }

    // A transient cause wrapped as an inner exception must still be detected via recursion.
    [Fact]
    public void IsMomentumErrorWalksInnerExceptions()
    {
        // arrange
        Exception exception = new InvalidOperationException("outer", new SocketException());

        // assert
        Assert.True(exception.IsMomentumError());
    }

    [Fact]
    public void CertificateValidationReturnsTrueWhenNoSslErrors()
    {
        // act
        bool result = ExceptionHelper.CertificateValidationCallBack(this, null, null, SslPolicyErrors.None);

        // assert
        Assert.True(result);
    }

    [Fact]
    public void CertificateValidationReturnsFalseForNonChainErrors()
    {
        // act — a name-mismatch (not a chain error) is not tolerated.
        bool result = ExceptionHelper.CertificateValidationCallBack(this, null, null,
            SslPolicyErrors.RemoteCertificateNameMismatch);

        // assert
        Assert.False(result);
    }

    [Fact]
    public void CertificateValidationToleratesChainErrorsWhenChainStatusIsNull()
    {
        // act — chain errors flagged but no detailed status available → tolerated.
        bool result = ExceptionHelper.CertificateValidationCallBack(this, null, chain: null,
            SslPolicyErrors.RemoteCertificateChainErrors);

        // assert
        Assert.True(result);
    }

    [Fact]
    public void CertificateValidationToleratesSelfSignedUntrustedRoot()
    {
        // arrange — a valid self-signed cert yields an UntrustedRoot chain status with
        // subject == issuer, which the callback tolerates.
        using X509Certificate2 cert = CreateSelfSignedCertificate(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        using X509Chain chain = BuildChain(cert);

        // act
        bool result = ExceptionHelper.CertificateValidationCallBack(this, cert, chain,
            SslPolicyErrors.RemoteCertificateChainErrors);

        // assert
        Assert.True(result);
    }

    [Fact]
    public void CertificateValidationToleratesExpiredCertificate()
    {
        // arrange — an expired self-signed cert yields a NotTimeValid chain status, which the
        // callback explicitly tolerates (continues on certificate expiration).
        using X509Certificate2 cert = CreateSelfSignedCertificate(
            DateTimeOffset.UtcNow.AddYears(-2), DateTimeOffset.UtcNow.AddYears(-1));
        using X509Chain chain = BuildChain(cert);

        // act
        bool result = ExceptionHelper.CertificateValidationCallBack(this, cert, chain,
            SslPolicyErrors.RemoteCertificateChainErrors);

        // assert
        Assert.True(result);
    }

    private static X509Certificate2 CreateSelfSignedCertificate(DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        using RSA rsa = RSA.Create(2048);
        CertificateRequest request = new("CN=DownloaderTest", rsa,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(notBefore, notAfter);
    }

    private static X509Chain BuildChain(X509Certificate2 certificate)
    {
        X509Chain chain = new();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
        chain.Build(certificate);
        return chain;
    }
}
