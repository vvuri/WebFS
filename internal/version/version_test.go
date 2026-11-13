package version

import "testing"

func TestStringNonEmpty(t *testing.T) {
	if got := String(); got == "" {
		t.Fatalf("String() = empty, want non-empty")
	}
}
