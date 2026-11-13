// Package main is the entry point of the WebFS CLI binary.
package main

import (
	"fmt"

	"github.com/vvuri/WebFS/internal/version"
)

func main() {
	fmt.Println("WebFS v" + version.String())
	fmt.Println()
}
