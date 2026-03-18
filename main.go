package main

import (
	"archive/zip"
	"bytes"
	"crypto/x509"
	"encoding/asn1"
	"encoding/pem"
	"flag"
	"io"
	"net/http"
	"os"
	"strings"
	"syscall"
	"time"
	"unsafe"
)

const (
	itiDefaultUrl = "http://acraiz.icpbrasil.gov.br/credenciadas/CertificadosAC-ICP-Brasil/ACcompactado.zip"
	mpfDefaultUrl = "http://repositorio.acinterna.mpf.mp.br/ejbca/ra/downloads/ACIMPF-cadeia-completa.p7b"
)

const (
	CERT_STORE_PROV_SYSTEM_W        = 10
	CERT_SYSTEM_STORE_LOCAL_MACHINE = 0x00020000
	X509_ASN_ENCODING               = 0x00000001
	PKCS_7_ASN_ENCODING             = 0x00010000
	CERT_STORE_ADD_REPLACE_EXISTING = 3
)

var (
	crypt32                              = syscall.NewLazyDLL("crypt32.dll")
	procCertOpenStore                    = crypt32.NewProc("CertOpenStore")
	procCertAddEncodedCertificateToStore = crypt32.NewProc("CertAddEncodedCertificateToStore")
	procCertCloseStore                   = crypt32.NewProc("CertCloseStore")
)

type contentInfo struct {
	ContentType asn1.ObjectIdentifier
	Content     asn1.RawValue `asn1:"explicit,tag:0,optional"`
}

type signedData struct {
	Version          int
	DigestAlgorithms []asn1.RawValue `asn1:"set"`
	ContentInfo      contentInfo
	Certificates     asn1.RawValue `asn1:"optional,tag:0"`
}

func main() {
	flagITI := flag.Bool("iti", false, "Install ITI certificates")
	flagMPF := flag.Bool("mpf", false, "Install MPF certificates")
	flagAll := flag.Bool("all", false, "Install both ITI and MPF certificates (default)")
	flagDryRun := flag.Bool("dry-run", false, "Simulate installation without writing to the store")
	flagQuiet := flag.Bool("q", false, "Quiet mode (suppress exit prompt)")

	flag.Usage = func() {
		println("Usage: WinCertInstaller [options]")
		println("Options:")
		flag.PrintDefaults()
		println("Example: WinCertInstaller --iti --dry-run")
	}
	flag.Parse()

	if len(os.Args) == 1 {
		*flagAll = true
	}

	installITI := *flagITI || *flagAll
	installMPF := *flagMPF || *flagAll

	if !installITI && !installMPF {
		println("Error: No certificate source selected.")
		flag.Usage()
		waitForExit(*flagQuiet)
		os.Exit(1)
	}

	if *flagDryRun {
		println("Dry run enabled: No changes will be made to the certificate store.")
	}

	if installITI {
		println("====================== ITI ======================")
		println("Downloading certificates from ", itiDefaultUrl, ". Please wait...")
		certs := downloadZIPCerts(itiDefaultUrl)
		installCerts(certs, *flagDryRun)
	}

	if installMPF {
		println("====================== MPF ======================")
		println("Downloading certificates from ", mpfDefaultUrl, ". Please wait...")
		certs := downloadP7BCerts(mpfDefaultUrl)
		installCerts(certs, *flagDryRun)
	}

	println("=================================================")
	println("Installation process completed.")
	waitForExit(*flagQuiet)
}

func downloadFile(url string) []byte {
	client := &http.Client{Timeout: 60 * time.Second}
	resp, err := client.Get(url)
	if err != nil {
		println("ERROR: Failed to download ", url, ": ", err.Error())
		return nil
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		println("ERROR: Bad status ", resp.Status, " for ", url)
		return nil
	}

	data, err := io.ReadAll(resp.Body)
	if err != nil {
		println("ERROR: Failed to read body from ", url, ": ", err.Error())
		return nil
	}
	return data
}

func downloadZIPCerts(url string) []*x509.Certificate {
	data := downloadFile(url)
	if data == nil {
		return nil
	}

	reader, err := zip.NewReader(bytes.NewReader(data), int64(len(data)))
	if err != nil {
		println("ERROR: Failed to open ZIP from ", url, ": ", err.Error())
		return nil
	}

	var parsedCerts []*x509.Certificate
	for _, file := range reader.File {
		if strings.HasSuffix(strings.ToLower(file.Name), ".cer") || strings.HasSuffix(strings.ToLower(file.Name), ".crt") {
			rc, err := file.Open()
			if err != nil {
				continue
			}
			cerData, err := io.ReadAll(rc)
			rc.Close()
			if err != nil {
				continue
			}

			cert, err := x509.ParseCertificate(cerData)
			if err != nil {
				continue
			}
			parsedCerts = append(parsedCerts, cert)
		}
	}
	println("Found ", len(parsedCerts), " certificate(s) in ZIP archive.")
	return parsedCerts
}

func downloadP7BCerts(url string) []*x509.Certificate {
	data := downloadFile(url)
	if data == nil {
		return nil
	}

	strData := string(data)
	if strings.Contains(strData, "-----BEGIN PKCS7-----") {
		block, _ := pem.Decode(data)
		if block == nil {
			println("ERROR: Failed to decode PEM PKCS7.")
			return nil
		}
		data = block.Bytes
	}

	var p7 struct {
		ContentType asn1.ObjectIdentifier
		Content     asn1.RawValue `asn1:"explicit,tag:0,optional"`
	}
	if _, err := asn1.Unmarshal(data, &p7); err != nil {
		println("ERROR: Failed to unmarshal PKCS7 wrapper: ", err.Error())
		return nil
	}

	var sd signedData
	if _, err := asn1.Unmarshal(p7.Content.Bytes, &sd); err != nil {
		println("ERROR: Failed to unmarshal signedData: ", err.Error())
		return nil
	}

	certs, err := x509.ParseCertificates(sd.Certificates.Bytes)
	if err != nil {
		println("ERROR: Failed to parse certificates from PKCS7: ", err.Error())
		return nil
	}

	println("Extracted ", len(certs), " certificate(s) from P7B/PKCS7 payload.")
	return certs
}

func installCerts(certs []*x509.Certificate, dryRun bool) {
	if len(certs) == 0 {
		return
	}

	var rootCerts []*x509.Certificate
	var caCerts []*x509.Certificate
	now := time.Now()

	for _, cert := range certs {
		if now.Before(cert.NotBefore) {
			println("WARNING: Certificate ", cert.Subject.CommonName, " not active yet.")
			continue
		}
		if now.After(cert.NotAfter) {
			println("WARNING: Certificate ", cert.Subject.CommonName, " expired.")
			continue
		}

		if cert.IsCA {
			if bytes.Equal(cert.RawSubject, cert.RawIssuer) {
				rootCerts = append(rootCerts, cert)
			} else {
				caCerts = append(caCerts, cert)
			}
		}
	}

	if len(rootCerts) > 0 {
		addToStore("Root", rootCerts, dryRun)
	} else {
		println("INFO: No CA certificates to import.")
	}

	if len(caCerts) > 0 {
		addToStore("CA", caCerts, dryRun)
	} else {
		println("INFO: No Intermediate CA certificates to import.")
	}
}

func addToStore(storeName string, certs []*x509.Certificate, dryRun bool) {
	println("Installing certificates into the ", storeName, " store...")

	var hStore uintptr
	if !dryRun {
		utf16StoreName, err := syscall.UTF16PtrFromString(storeName)
		if err != nil {
			println("ERROR: Store name conversion failed: ", err.Error())
			return
		}
		hStore, _, err = procCertOpenStore.Call(
			uintptr(CERT_STORE_PROV_SYSTEM_W),
			0, // dwEncodingType
			0, // hCryptProv
			uintptr(CERT_SYSTEM_STORE_LOCAL_MACHINE),
			uintptr(unsafe.Pointer(utf16StoreName)),
		)
		if hStore == 0 {
			println("ERROR: Error accessing Windows certificate store: ", err.Error())
			println("HINT: Ensure you are running this application as 'Administrator' to manage LocalMachine certificates.")
			return
		}
		defer procCertCloseStore.Call(hStore, 0)
	}

	added := 0
	for _, cert := range certs {
		if dryRun {
			println("Dry-run: Would add ", cert.Subject.CommonName, " to ", storeName)
			added++
			continue
		}

		ret, _, _ := procCertAddEncodedCertificateToStore.Call(
			hStore,
			uintptr(X509_ASN_ENCODING|PKCS_7_ASN_ENCODING),
			uintptr(unsafe.Pointer(&cert.Raw[0])),
			uintptr(len(cert.Raw)),
			uintptr(CERT_STORE_ADD_REPLACE_EXISTING),
			0, // ppCertContext
		)
		if ret != 0 {
			println("Added: ", cert.Subject.CommonName, " to ", storeName)
			added++
		} else {
			println("WARNING: Failed to add ", cert.Subject.CommonName, " to ", storeName)
		}
	}
	println("Successfully processed ", added, " certificate(s) for ", storeName, ".")
}

func waitForExit(quiet bool) {
	if !quiet {
		println("\nUse the -q parameter to run without this prompt.")
		print("Press Enter to exit...")
		var b []byte = make([]byte, 1)
		os.Stdin.Read(b)
	}
}
