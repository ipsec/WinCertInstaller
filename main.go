package main

import (
	"archive/zip"
	"bytes"
	"crypto/x509"
	"encoding/asn1"
	"encoding/pem"
	"flag"
	"fmt"
	"io"
	"log"
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
		fmt.Println("Usage: WinCertInstaller [options]")
		fmt.Println("Options:")
		flag.PrintDefaults()
		fmt.Println("Example: WinCertInstaller --iti --dry-run")
	}
	flag.Parse()

	if len(os.Args) == 1 {
		*flagAll = true
	}

	installITI := *flagITI || *flagAll
	installMPF := *flagMPF || *flagAll

	if !installITI && !installMPF {
		fmt.Println("Error: No certificate source selected.")
		flag.Usage()
		waitForExit(*flagQuiet)
		os.Exit(1)
	}

	if *flagDryRun {
		log.Println("Dry run enabled: No changes will be made to the certificate store.")
	}

	if installITI {
		log.Println("====================== ITI ======================")
		log.Printf("Downloading certificates from %s. Please wait...\n", itiDefaultUrl)
		certs := downloadZIPCerts(itiDefaultUrl)
		installCerts(certs, *flagDryRun)
	}

	if installMPF {
		log.Println("====================== MPF ======================")
		log.Printf("Downloading certificates from %s. Please wait...\n", mpfDefaultUrl)
		certs := downloadP7BCerts(mpfDefaultUrl)
		installCerts(certs, *flagDryRun)
	}

	log.Println("=================================================")
	log.Println("Installation process completed.")
	waitForExit(*flagQuiet)
}

func downloadFile(url string) []byte {
	client := &http.Client{Timeout: 60 * time.Second}
	resp, err := client.Get(url)
	if err != nil {
		log.Printf("ERROR: Failed to download %s: %v\n", url, err)
		return nil
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		log.Printf("ERROR: Bad status %s for %s\n", resp.Status, url)
		return nil
	}

	data, err := io.ReadAll(resp.Body)
	if err != nil {
		log.Printf("ERROR: Failed to read body from %s: %v\n", url, err)
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
		log.Printf("ERROR: Failed to open ZIP from %s: %v\n", url, err)
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
	log.Printf("Found %d certificate(s) in ZIP archive.\n", len(parsedCerts))
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
			log.Println("ERROR: Failed to decode PEM PKCS7.")
			return nil
		}
		data = block.Bytes
	}

	var p7 struct {
		ContentType asn1.ObjectIdentifier
		Content     asn1.RawValue `asn1:"explicit,tag:0,optional"`
	}
	if _, err := asn1.Unmarshal(data, &p7); err != nil {
		log.Printf("ERROR: Failed to unmarshal PKCS7 wrapper: %v\n", err)
		return nil
	}

	var sd signedData
	if _, err := asn1.Unmarshal(p7.Content.Bytes, &sd); err != nil {
		log.Printf("ERROR: Failed to unmarshal signedData: %v\n", err)
		return nil
	}

	certs, err := x509.ParseCertificates(sd.Certificates.Bytes)
	if err != nil {
		log.Printf("ERROR: Failed to parse certificates from PKCS7: %v\n", err)
		return nil
	}

	log.Printf("Extracted %d certificate(s) from P7B/PKCS7 payload.\n", len(certs))
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
			log.Printf("WARNING: Certificate %s not active yet.\n", cert.Subject.CommonName)
			continue
		}
		if now.After(cert.NotAfter) {
			log.Printf("WARNING: Certificate %s expired.\n", cert.Subject.CommonName)
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
		log.Println("INFO: No CA certificates to import.")
	}

	if len(caCerts) > 0 {
		addToStore("CA", caCerts, dryRun)
	} else {
		log.Println("INFO: No Intermediate CA certificates to import.")
	}
}

func addToStore(storeName string, certs []*x509.Certificate, dryRun bool) {
	log.Printf("Installing certificates into the %s store...\n", storeName)

	var hStore uintptr
	if !dryRun {
		utf16StoreName, err := syscall.UTF16PtrFromString(storeName)
		if err != nil {
			log.Printf("ERROR: Store name conversion failed: %v\n", err)
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
			log.Printf("ERROR: Error accessing Windows certificate store: %v\n", err)
			log.Println("HINT: Ensure you are running this application as 'Administrator' to manage LocalMachine certificates.")
			return
		}
		defer procCertCloseStore.Call(hStore, 0)
	}

	added := 0
	for _, cert := range certs {
		if dryRun {
			log.Printf("Dry-run: Would add %s to %s\n", cert.Subject.CommonName, storeName)
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
			log.Printf("Added: %s to %s\n", cert.Subject.CommonName, storeName)
			added++
		} else {
			log.Printf("WARNING: Failed to add %s to %s\n", cert.Subject.CommonName, storeName)
		}
	}
	log.Printf("Successfully processed %d certificate(s) for %s.\n", added, storeName)
}

func waitForExit(quiet bool) {
	if !quiet {
		fmt.Println("\nUse the -q parameter to run without this prompt.")
		fmt.Print("Press Enter to exit...")
		var b []byte = make([]byte, 1)
		os.Stdin.Read(b)
	}
}
