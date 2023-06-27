defmodule TimebotTest do
  use ExUnit.Case
  doctest Timebot

  test "greets the world" do
    assert Timebot.hello() == :world
  end
end
