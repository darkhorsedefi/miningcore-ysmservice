CC = gcc
CXX = g++
CFLAGS = -g -Wall -c -fPIC -O2 -Wno-pointer-sign -Wno-char-subscripts -Wno-unused-variable -Wno-unused-function -Wno-strict-aliasing -Wno-discarded-qualifiers -Wno-unused-const-variable
CXXFLAGS = -g -Wall -fPIC -fpermissive -O2 -Wno-char-subscripts -Wno-unused-variable -Wno-unused-function -Wno-strict-aliasing -Wno-sign-compare -std=c++11
LDFLAGS = -shared
LDLIBS = -lsodium
TARGET  = libxehash.so

OBJECTS = xehash.o

all: $(TARGET)

$(TARGET): $(OBJECTS)
	$(CXX) $(LDFLAGS) -o $@ $^ $(LDLIBS)

.cpp.o:
	$(CXX) $(CXXFLAGS) -c $<

clean:
	$(RM) $(TARGET) *.o
