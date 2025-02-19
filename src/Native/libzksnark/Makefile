CC = g++
CFLAGS = -Wall -fPIC -O2 -std=c++17 -DCURVE_BN128
CXXFLAGS = $(CFLAGS)
LDFLAGS = -shared
LDLIBS = -lgmp -lgmpxx -lsnark

# libsnarkの依存関係を含める
INCLUDE_PATHS = -I/usr/local/include -I/usr/include/libsnark -I/usr/include/libff
LIBRARY_PATHS = -L/usr/local/lib

TARGET  = libzksnark.so
OBJECTS = zksnark.o

all: $(TARGET)

$(TARGET): $(OBJECTS)
	$(CC) $(LDFLAGS) -o $@ $^ $(LIBRARY_PATHS) $(LDLIBS)

%.o: %.cpp
	$(CC) $(CXXFLAGS) $(INCLUDE_PATHS) -c $<

clean:
	$(RM) $(TARGET) $(OBJECTS)

.PHONY: all clean